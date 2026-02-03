/**
 * 文件哈希工具类
 * 使用 SparkMD5 计算文件的 MD5 哈希值
 */

/**
 * 计算文件的 MD5 哈希值
 * @param file 要计算的文件对象
 * @returns Promise<string> MD5 哈希值（32位小写十六进制字符串）
 */
export async function calculateFileHash(file: File): Promise<string> {
  // 动态导入 SparkMD5
  const SparkMD5 = (await import('spark-md5')).default;

  return new Promise((resolve, reject) => {
    const spark = new SparkMD5.ArrayBuffer();
    const fileReader = new FileReader();
    const chunkSize = 2 * 1024 * 1024; // 2MB 分块读取
    let chunks = 0;
    const totalChunks = Math.ceil(file.size / chunkSize);

    const loadNext = () => {
      const start = chunks * chunkSize;
      const end = Math.min(start + chunkSize, file.size);
      const chunk = file.slice(start, end);

      fileReader.onload = (e: any) => {
        try {
          spark.append(e.target.result);
          chunks++;

          if (chunks < totalChunks) {
            // 继续读取下一块
            loadNext();
          } else {
            // 读取完成，计算哈希
            const hash = spark.end();
            resolve(hash);
          }
        } catch (error) {
          reject(error);
        }
      };

      fileReader.onerror = () => reject(fileReader.error);
      fileReader.readAsArrayBuffer(chunk);
    };

    loadNext();
  });
}

/**
 * 计算字符串的 MD5 哈希值
 * @param str 要计算的字符串
 * @returns MD5 哈希值（32位小写十六进制字符串）
 */
export async function calculateStringHash(str: string): Promise<string> {
  const SparkMD5 = (await import('spark-md5')).default;
  return SparkMD5.hash(str);
}
