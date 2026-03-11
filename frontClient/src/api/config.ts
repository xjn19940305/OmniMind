import { request } from "../utils/request";

export interface ModelConfigResponse {
  chatModels?: string[];
  embeddingModel?: string;
  vectorSize?: number;
  maxTokens?: number;
  temperature?: number;
  topP?: number;
}

export function getModelConfig() {
  return request<ModelConfigResponse>({
    url: "/api/Config/models",
    method: "get",
  });
}
