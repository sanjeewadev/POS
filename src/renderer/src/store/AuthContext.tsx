// src/renderer/src/store/AuthContext.tsx
import React, { createContext, useContext, useState, useEffect } from 'react'
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

  // 🚀 NEW: Touch Numpad Global State & Controllers
  posNumpadEnabled: boolean
  globalNumpadEnabled: boolean
  togglePosNumpad: (enabled: boolean) => Promise<void>
  toggleGlobalNumpad: (enabled: boolean) => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [currentUser, setCurrentUser] = useState<User | null>(null)

  // 🚀 NEW: Numpad State (Defaults to false until loaded from DB)
  const [posNumpadEnabled, setPosNumpadEnabled] = useState(false)
  const [globalNumpadEnabled, setGlobalNumpadEnabled] = useState(false)

  // 🚀 NEW: Load Interface Settings on App Startup
  useEffect(() => {
    const loadInterfaceSettings = async () => {
      try {
        // @ts-ignore
        const data = await window.api.getSettings()
        if (data) {
          // Convert SQLite 1/0 to boolean
          setPosNumpadEnabled(data.EnablePosNumpad === 1)
          setGlobalNumpadEnabled(data.EnableGlobalNumpad === 1)
        }
      } catch (error) {
        console.error('Failed to load interface settings:', error)
      }
    }
    loadInterfaceSettings()
  }, [])

  // 🚀 NEW: Functions to update Numpad state and save to DB
  const togglePosNumpad = async (enabled: boolean) => {
    setPosNumpadEnabled(enabled)
    try {
      // @ts-ignore
      await window.api.updateSettings({ EnablePosNumpad: enabled ? 1 : 0 })
    } catch (err) {
      console.error('Failed to save POS Numpad setting:', err)
    }
  }

  const toggleGlobalNumpad = async (enabled: boolean) => {
    setGlobalNumpadEnabled(enabled)
    try {
      // @ts-ignore
      await window.api.updateSettings({ EnableGlobalNumpad: enabled ? 1 : 0 })
    } catch (err) {
      console.error('Failed to save Global Numpad setting:', err)
    }
  }

  // THE GHOST FOCUS KILLER
  const releaseGhostFocus = () => {
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur()
    }
  }

  const login = async (username: string, pass: string) => {
    try {
      releaseGhostFocus()

      const cleanUsername = username.trim()
      const cleanPass = pass.trim()

      // --- 1. PERMANENT SUPER ADMIN (The "Root" Account) ---
      if (cleanUsername === 'master_admin' && cleanPass === 'kj%gs6s%s8*7t') {
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
    <AuthContext.Provider
      value={{
        currentUser,
        login,
        logout,
        hasPermission,
        posNumpadEnabled,
        globalNumpadEnabled,
        togglePosNumpad,
        toggleGlobalNumpad
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) throw new Error('useAuth must be used within an AuthProvider')
  return context
}
