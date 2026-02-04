import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('../views/Login.vue'),
    meta: { requiresAuth: false, title: '登录' }
  },
  {
    path: '/',
    name: 'Layout',
    component: () => import('../views/Layout.vue'),
    redirect: '/chat',
    meta: { requiresAuth: true },
    children: [
      {
        path: '/chat',
        name: 'Chat',
        component: () => import('../views/Chat.vue'),
        meta: { requiresAuth: true, title: 'AI 对话' }
      },
      {
        path: '/knowledge',
        name: 'Knowledge',
        component: () => import('../views/Knowledge.vue'),
        meta: { requiresAuth: true, title: '知识库' }
      },
      {
        path: '/knowledge/:id',
        name: 'KnowledgeDetail',
        component: () => import('../views/KnowledgeDetail.vue'),
        meta: { requiresAuth: true, title: '知识库详情' }
      },
      {
        path: '/profile',
        name: 'Profile',
        component: () => import('../views/Profile.vue'),
        meta: { requiresAuth: true, title: '个人中心' }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    redirect: '/chat'
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
})

// Navigation guard
router.beforeEach((to, _from, next) => {
  const token = localStorage.getItem('token')

  if (to.meta.requiresAuth && !token) {
    next('/login')
  } else if (to.path === '/login' && token) {
    next('/chat')
  } else {
    next()
  }
})

export default router
