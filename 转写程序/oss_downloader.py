#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import io
import json
import logging
import os
import configparser
from typing import Optional, Tuple
from urllib.parse import urlparse

from minio import Minio
from minio.error import S3Error

logger = logging.getLogger(__name__)


class OSSClient:
    """
    S3-compatible object storage client used by the transcription worker.

    The class name is kept for compatibility with the existing consumer code.
    """

    def __init__(self, config_file: str = "config.ini"):
        self.config = configparser.ConfigParser()
        self.config.read(config_file, encoding="utf-8")

        self.endpoint = self._get_config("storage", "endpoint", fallback_section="oss")
        self.bucket_name = self._get_config("storage", "bucket", fallback_section="oss")
        self.access_key_id = self._get_config(
            "storage", "access_key_id", fallback_section="oss"
        )
        self.access_key_secret = self._get_config(
            "storage", "access_key_secret", fallback_section="oss"
        )
        self.secure = self._resolve_secure()

        self.temp_dir = self._get_config(
            "storage", "temp_dir", fallback_section="oss", fallback="temp_oss_files"
        )
        os.makedirs(self.temp_dir, exist_ok=True)

        endpoint = self._normalize_endpoint(self.endpoint)
        self.client = Minio(
            endpoint,
            access_key=self.access_key_id,
            secret_key=self.access_key_secret,
            secure=self.secure,
        )

        logger.info("Object storage client initialized")
        logger.info("  Endpoint: %s", endpoint)
        logger.info("  Bucket: %s", self.bucket_name)
        logger.info("  Secure: %s", self.secure)

    def _get_config(
        self,
        section: str,
        key: str,
        fallback_section: Optional[str] = None,
        fallback: Optional[str] = None,
    ) -> str:
        if self.config.has_option(section, key):
            return self.config.get(section, key)
        if fallback_section and self.config.has_option(fallback_section, key):
            return self.config.get(fallback_section, key)
        if fallback is not None:
            return fallback
        raise KeyError(f"Missing config value: [{section}] {key}")

    def _resolve_secure(self) -> bool:
        if self.config.has_option("storage", "secure"):
            return self.config.getboolean("storage", "secure")
        if self.config.has_option("oss", "secure"):
            return self.config.getboolean("oss", "secure")
        endpoint = self.endpoint.lower()
        return endpoint.startswith("https://")

    @staticmethod
    def _normalize_endpoint(endpoint: str) -> str:
        parsed = urlparse(endpoint)
        if parsed.scheme:
            return parsed.netloc
        return endpoint

    def _ensure_bucket_exists(self) -> None:
        if not self.client.bucket_exists(self.bucket_name):
            self.client.make_bucket(self.bucket_name)

    def download_file(
        self, object_key: str, local_filename: Optional[str] = None
    ) -> Tuple[bool, str, Optional[bytes]]:
        try:
            stat = self.client.stat_object(self.bucket_name, object_key)
            logger.info("Downloading object: %s", object_key)
            logger.info("  Size: %.2f MB", stat.size / (1024 * 1024))

            if local_filename is None:
                local_filename = os.path.basename(object_key) or "downloaded_file"

            safe_filename = "".join(
                c for c in local_filename if c.isalnum() or c in "._-"
            ) or "downloaded_file"
            local_path = os.path.join(self.temp_dir, safe_filename)

            if os.path.exists(local_path):
                name, ext = os.path.splitext(safe_filename)
                local_path = os.path.join(
                    self.temp_dir, f"{name}_{int(os.path.getmtime(local_path))}{ext}"
                )

            response = self.client.get_object(self.bucket_name, object_key)
            try:
                content = response.read()
            finally:
                response.close()
                response.release_conn()

            with open(local_path, "wb") as file:
                file.write(content)

            logger.info("Object download completed: %s", local_path)
            return True, local_path, content
        except S3Error as ex:
            logger.error("Failed to download object %s: %s", object_key, ex)
            return False, "", None
        except Exception as ex:
            logger.error("Failed to download object %s: %s", object_key, ex, exc_info=True)
            return False, "", None

    def upload_text(self, content: str, object_key: str) -> Tuple[bool, Optional[str]]:
        try:
            self._ensure_bucket_exists()
            payload = content.encode("utf-8")
            self.client.put_object(
                self.bucket_name,
                object_key,
                io.BytesIO(payload),
                len(payload),
                content_type="text/plain; charset=utf-8",
            )
            return True, object_key
        except Exception as ex:
            logger.error("Failed to upload text %s: %s", object_key, ex, exc_info=True)
            return False, None

    def upload_json_raw(
        self, content: str, object_key: str
    ) -> Tuple[bool, Optional[str]]:
        try:
            self._ensure_bucket_exists()
            payload = content.encode("utf-8")
            self.client.put_object(
                self.bucket_name,
                object_key,
                io.BytesIO(payload),
                len(payload),
                content_type="application/json; charset=utf-8",
            )
            return True, object_key
        except Exception as ex:
            logger.error("Failed to upload json %s: %s", object_key, ex, exc_info=True)
            return False, None

    def upload_transcription_result(
        self,
        source_object_key: str,
        transcription_data: dict,
        user_id: str = "default",
        document_id: str = "0",
    ) -> Tuple[bool, Optional[str]]:
        del source_object_key
        result_object_key = f"transcription/{user_id}/{document_id}.json"
        content = self._format_transcription_json(transcription_data)
        return self.upload_json_raw(content, result_object_key)

    def _format_transcription_json(self, data: dict) -> str:
        segments = data.get("Segments", [])
        speakers = data.get("Speakers", [])
        duration_ms = data.get("DurationMs", 0)
        processing_time = data.get("ProcessingTime", 0)
        full_text = data.get("Text", "")

        formatted_segments = []
        for seg in segments:
            start = seg.get("StartTime", 0)
            end = seg.get("EndTime", 0)
            formatted_segments.append(
                {
                    "speaker": seg.get("Speaker", "Unknown"),
                    "text": seg.get("Text", ""),
                    "startTime": self._format_time_ms(start),
                    "endTime": self._format_time_ms(end),
                    "startMs": start,
                    "endMs": end,
                }
            )

        result = {
            "speakerCount": len(speakers),
            "segmentCount": len(segments),
            "audioDuration": round(duration_ms / 1000, 2),
            "processingTime": round(processing_time, 2),
            "fullText": full_text,
            "segments": formatted_segments,
        }

        return json.dumps(result, ensure_ascii=False, indent=2)

    @staticmethod
    def _format_time_ms(milliseconds: int) -> str:
        total_seconds = max(0, int(milliseconds / 1000))
        hours = total_seconds // 3600
        minutes = (total_seconds % 3600) // 60
        seconds = total_seconds % 60
        return f"{hours:02d}:{minutes:02d}:{seconds:02d}"
