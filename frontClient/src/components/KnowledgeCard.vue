<template>
  <div
    class="kb-card"
    :class="{ active: active }"
    @click="$emit('select', kb.id)"
  >
    <div class="kb-card-header">
      <div class="kb-icon-wrapper">
        <el-icon class="kb-icon" :size="28">
          <User v-if="kb.visibility === 1" />
          <Collection v-else />
        </el-icon>
      </div>
      <el-dropdown @command="(cmd) => $emit('command', cmd, kb)" trigger="click" @click.stop>
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
        <div class="kb-meta">
          <span class="meta-item">
            <el-icon><User /></el-icon>
            {{ kb.memberCount || 0 }} 成员
          </span>
          <span class="meta-item">
            {{ formatDate(kb.createdAt) }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { User, Collection, MoreFilled, FolderOpened, Edit, Delete } from '@element-plus/icons-vue'
import type { KnowledgeBase } from '../types'

defineProps<{
  kb: KnowledgeBase
  active: boolean
  isOwner: boolean
}>()

defineEmits<{
  select: [id: string]
  command: [cmd: string, kb: KnowledgeBase]
}>()

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) return '今天'
  if (days === 1) return '昨天'
  if (days < 7) return `${days}天前`
  if (days < 30) return `${Math.floor(days / 7)}周前`
  if (days < 365) return `${Math.floor(days / 30)}月前`
  return `${Math.floor(days / 365)}年前`
}
</script>

<style scoped lang="scss">
.kb-card {
  border: 1px solid #e4e7ed;
  border-radius: 12px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.25s ease;
  background: #fff;
  display: flex;
  flex-direction: column;
  min-height: 160px;
  position: relative;

  &:hover {
    border-color: #409eff;
    box-shadow: 0 4px 20px rgba(64, 158, 255, 0.15);
    transform: translateY(-2px);
  }

  &.active {
    border-color: #409eff;
    background: linear-gradient(135deg, #ecf5ff 0%, #f0f9ff 100%);
    box-shadow: 0 4px 16px rgba(64, 158, 255, 0.2);
  }
}

.kb-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.kb-icon-wrapper {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #409eff 0%, #66b1ff 100%);

  .kb-icon {
    color: #fff;
  }
}

.more-btn {
  padding: 4px;
  color: #909399;

  &:hover {
    color: #409eff;
  }
}

.kb-card-body {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.kb-name {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 8px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.kb-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-bottom: 8px;
}

.kb-desc {
  font-size: 13px;
  color: #606266;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 12px;
  flex: 1;
}

.kb-footer {
  margin-top: auto;
}

.kb-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #909399;

  .meta-item {
    display: flex;
    align-items: center;
    gap: 4px;
  }
}
</style>
