import axios from 'axios'
import { useStore } from '../store/useStore'

export const api = axios.create({ baseURL: '/api' })

api.interceptors.response.use(
  res => res,
  err => {
    if (!err.response || err.response.status >= 500) {
      useStore.getState().addToast(err.response?.data?.title ?? 'Server error')
    }
    return Promise.reject(err)
  }
)
