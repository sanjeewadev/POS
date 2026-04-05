// src/renderer/src/views/Login/LoginView.tsx
import React, { useState } from 'react'
import { useAuth } from '../../store/AuthContext'
import styles from './LoginView.module.css'

export default function LoginView() {
  const { login } = useAuth()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isBusy, setIsBusy] = useState(false)

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault()
    if (isBusy) return

    if (!username || !password) {
      setError('⚠️ Username and password are required.')
      return
    }

    setIsBusy(true)
    setError('Verifying credentials...')

    const result = await login(username, password)

    if (!result.success) {
      setError(result.error || 'Login failed.')
      setPassword('')
      setIsBusy(false)
    }
    // If successful, the AuthContext updates and App.tsx changes the screen!
  }

  const handleExit = () => {
    window.close()
  }

  return (
    <div className={styles.container}>
      <div className={styles.loginCard}>
        {/* Header */}
        <div className={styles.header}>
          <div className={styles.brand}>
            UNIVERSAL<span>POS</span>
          </div>
          <p className={styles.subtitle}>Enter your credentials to access the terminal.</p>
        </div>

        {/* Form */}
        <form onSubmit={handleLogin} className={styles.form}>
          <div className={styles.inputGroup}>
            <label>USERNAME</label>
            <input
              type="text"
              className="pos-input"
              value={username}
              onChange={(e) => {
                setUsername(e.target.value)
                setError('')
              }}
              readOnly={isBusy}
              placeholder="e.g. admin"
              autoFocus
            />
          </div>

          <div className={styles.inputGroup}>
            <label>PASSWORD</label>
            <input
              type="password"
              className="pos-input"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setError('')
              }}
              readOnly={isBusy}
              placeholder="••••••••"
            />
          </div>

          {/* Error Message */}
          <div className={styles.errorContainer}>
            {error && (
              <span className={error.includes('Verifying') ? styles.infoText : styles.errorText}>
                {error}
              </span>
            )}
          </div>

          {/* Action Buttons */}
          <div className={styles.actionGrid}>
            <button
              type="submit"
              className="pos-btn success"
              disabled={isBusy}
              style={{ width: '100%' }}
            >
              {isBusy ? 'AUTHENTICATING...' : 'SECURE LOGIN'}
            </button>

            <button
              type="button"
              className="pos-btn neutral"
              onClick={handleExit}
              disabled={isBusy}
              style={{ width: '100%', backgroundColor: '#e2e8f0', color: 'var(--text-dark)' }}
            >
              EXIT TO DESKTOP
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
