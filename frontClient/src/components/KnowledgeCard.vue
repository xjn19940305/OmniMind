<template>
  <div
    class="kb-card"
    :class="{ active }"
    @click="$emit('select', kb.id)"
  >
    <div class="kb-card-header">
      <div class="kb-icon-wrapper">
        <el-icon class="kb-icon" :size="28">
          <User v-if="kb.visibility === 1" />
          <Collection v-else />
        </el-icon>
      </div>

      <el-dropdown
        trigger="click"
        @command="(cmd) => $emit('command', cmd, kb)"
        @click.stop
      >
        <el-button text class="more-btn">
          <el-icon><MoreFilled /></el-icon>
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="open">
              <el-icon><FolderOpened /></el-icon> 打开
            </el-dropdown-item>
            <el-dropdown-item command="edit" :disabled="kb.visibility === 1 || !isOwner">
              <el-icon><Edit /></el-icon> 编辑
            </el-dropdown-item>
            <el-dropdown-item command="delete" :disabled="kb.visibility === 1 || !isOwner" divided>
              <el-icon><Delete /></el-icon> 删除
            </el-dropdown-item>
            <el-dropdown-item command="members" :disabled="kb.visibility === 1" divided>
              <el-icon><User /></el-icon> 成员管理
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>

    <div class="kb-card-body">
      <div class="kb-name">{{ kb.name }}</div>

      <div class="kb-tags">
        <el-tag v-if="kb.visibility === 1" type="danger" size="small">私有</el-tag>
        <el-tag v-else-if="kb.visibility === 2" type="info" size="small">内部</el-tag>
        <el-tag v-else type="success" size="small">公开</el-tag>
        <el-tag v-if="!isOwner && kb.ownerName" type="info" size="small" plain>
          {{ kb.ownerName }}
        </el-tag>
      </div>

      <div v-if="kb.description" class="kb-desc">{{ kb.description }}</div>

      <div class="kb-footer">
        <div class="kb-meta-list">
          <span class="meta-pill">
            <el-icon><User /></el-icon>
            {{ kb.memberCount || 0 }} 成员
          </span>
          <span class="meta-pill">
            {{ formatDate(kb.createdAt) }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  Collection,
  Delete,
  Edit,
  FolderOpened,
  MoreFilled,
  User,
} from "@element-plus/icons-vue";
import type { KnowledgeBase } from "../types";

defineProps<{
  kb: KnowledgeBase;
  active: boolean;
  isOwner: boolean;
}>();

defineEmits<{
  select: [id: string];
  command: [cmd: string, kb: KnowledgeBase];
}>();

function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return "今天";
  if (days === 1) return "昨天";
  if (days < 7) return `${days}天前`;
  if (days < 30) return `${Math.floor(days / 7)}周前`;
  if (days < 365) return `${Math.floor(days / 30)}月前`;
  return `${Math.floor(days / 365)}年前`;
}
</script>

<style scoped lang="scss">
.kb-card {
  position: relative;
  display: flex;
  min-height: 188px;
  flex-direction: column;
  padding: 18px;
  border: 1px solid var(--app-border);
  border-radius: 22px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(244, 248, 255, 0.94));
  box-shadow: 0 14px 30px rgba(15, 23, 42, 0.06);
  cursor: pointer;
  transition:
    transform 0.22s ease,
    box-shadow 0.22s ease,
    border-color 0.22s ease;

  &:hover {
    border-color: rgba(37, 99, 235, 0.26);
    box-shadow: 0 20px 36px rgba(37, 99, 235, 0.12);
    transform: translateY(-3px);
  }

  &.active {
    border-color: rgba(37, 99, 235, 0.4);
    background: linear-gradient(135deg, rgba(232, 240, 255, 0.96), rgba(241, 249, 255, 0.98));
    box-shadow: 0 22px 40px rgba(37, 99, 235, 0.16);
  }
}

.kb-card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 14px;
}

.kb-icon-wrapper {
  display: flex;
  width: 52px;
  height: 52px;
  align-items: center;
  justify-content: center;
  border-radius: 16px;
  background: linear-gradient(135deg, #2563eb 0%, #38bdf8 100%);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.28);

  .kb-icon {
    color: #fff;
  }
}

.more-btn {
  min-width: 38px;
  min-height: 38px;
  border-radius: 12px;
  color: var(--app-text-muted);

  &:hover {
    color: var(--app-primary-strong);
    background: rgba(37, 99, 235, 0.08);
  }
}

.kb-card-body {
  display: flex;
  flex: 1;
  flex-direction: column;
}

.kb-name {
  margin-bottom: 10px;
  color: var(--app-text);
  font-size: 17px;
  font-weight: 700;
  line-height: 1.35;
  word-break: break-word;
}

.kb-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 10px;
}

.kb-desc {
  display: -webkit-box;
  margin-bottom: 16px;
  overflow: hidden;
  color: var(--app-text-secondary);
  font-size: 13px;
  line-height: 1.65;
  word-break: break-word;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 3;
}

.kb-footer {
  margin-top: auto;
}

.kb-meta-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.meta-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 30px;
  padding: 0 10px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.82);
  color: var(--app-text-muted);
  font-size: 12px;
  word-break: break-word;
}

@media (max-width: 768px) {
  .kb-card {
    min-height: 172px;
    padding: 16px;
    border-radius: 18px;
  }

  .kb-name {
    font-size: 16px;
  }
}
</style>
