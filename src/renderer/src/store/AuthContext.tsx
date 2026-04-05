// src/renderer/src/store/AuthContext.tsx
import React, { createContext, useContext, useState } from 'react'
import { User } from '../types/models'

// SHA-256 Hashing converted to Base64
async function hashPassword(password: string): Promise<string> {
  if (!password) return ''
  const msgBuffer = new TextEncoder().encode(password)
  const hashBuffer = await crypto.subtle.digest('SHA-256', msgBuffer)
  const hashArray = Array.from(new Uint8Array(hashBuffer))
  const binaryString = String.fromCharCode(...hashArray)
  return btoa(binaryString)
}

interface AuthContextType {
  currentUser: User | null
  login: (username: string, pass: string) => Promise<{ success: boolean; error?: string }>
  logout: () => void
  hasPermission: (permission: string) => boolean
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [currentUser, setCurrentUser] = useState<User | null>(null)

  // 🚀 THE GHOST FOCUS KILLER
  const releaseGhostFocus = () => {
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur()
    }
    // 🗑️ REMOVED THE IPC SIGNAL TO PREVENT WINDOWS FROM LOCKING THE KEYBOARD
  }

  const login = async (username: string, pass: string) => {
    try {
      // 🚀 Drop the keyboard focus immediately before we start checking credentials
      releaseGhostFocus()

      const cleanUsername = username.trim()
      const cleanPass = pass.trim()

      // --- 1. PERMANENT SUPER ADMIN (The "Root" Account) ---
      if (cleanUsername === 'Super_admin-jh' && cleanPass === 'kj%gs6s%s8*7t') {
        setCurrentUser({
          Id: -1,
          Username: 'master_admin',
          PasswordHash: 'ROOT_NO_HASH',
          FullName: 'SYSTEM ROOT',
          Role: 0, // UserRole.SuperAdmin
          IsActive: true,
          Permissions: 'ALL'
        })
        return { success: true }
      }

      // 2. Get user from SQLite
      // @ts-ignore
      const user: User = await window.api.getUserByUsername(cleanUsername)

      if (!user) return { success: false, error: '❌ Invalid username or password.' }

      // 3. Security: Check if Account is Blocked
      if (!user.IsActive) {
        return { success: false, error: '⛔ Your account has been disabled.' }
      }

      // 4. Hash and check password
      const hashedAttempt = await hashPassword(cleanPass)
      if (user.PasswordHash !== hashedAttempt)
        return { success: false, error: '❌ Invalid username or password.' }

      // 5. Success! Save to session
      setCurrentUser(user)
      return { success: true }
    } catch (err: any) {
      return { success: false, error: `System Error: ${err.message}` }
    }
  }

  const logout = () => {
    // 🚀 Drop the keyboard focus right before destroying the POS screen
    releaseGhostFocus()
    setCurrentUser(null)
  }

  const hasPermission = (targetPermission: string) => {
    if (!currentUser) return false
    if (currentUser.Role === 0) return true // SuperAdmin always allowed
    if (!currentUser.Permissions) return false
    return (
      currentUser.Permissions.includes('ALL') || currentUser.Permissions.includes(targetPermission)
    )
  }

  return (
    <AuthContext.Provider value={{ currentUser, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) throw new Error('useAuth must be used within an AuthProvider')
  return context
}
