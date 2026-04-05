// src/renderer/src/pages/Settings/UserManager.tsx
import { useState, useEffect } from 'react'
import Swal from 'sweetalert2'
import { User } from '../../types/models'
import { useAuth } from '../../store/AuthContext'
import styles from './UserManager.module.css'

const STAFF_PERMISSIONS = [
  'POS',
  'Returns',
  'ViewProducts',
  'InventoryAlerts',
  'TodaySales',
  'SalesHistory',
  'CreditAccounts'
].join(',')

async function hashPassword(password: string): Promise<string> {
  if (!password) return ''
  const msgBuffer = new TextEncoder().encode(password)
  const hashBuffer = await crypto.subtle.digest('SHA-256', msgBuffer)
  const hashArray = Array.from(new Uint8Array(hashBuffer))
  const binaryString = String.fromCharCode(...hashArray)
  return btoa(binaryString)
}

export default function UserManager() {
  const { currentUser } = useAuth()
  const [users, setUsers] = useState<User[]>([])
  const [search, setSearch] = useState('')

  // Form State
  const [editingId, setEditingId] = useState<number | null>(null)
  const [fullName, setFullName] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [existingHash, setExistingHash] = useState('')
  const [role, setRole] = useState<number>(2)
  const [isActive, setIsActive] = useState<boolean>(true)

  const loadUsers = async () => {
    try {
      // @ts-ignore
      const data = await window.api.getUsers()
      setUsers(data || [])
    } catch (error) {
      console.error('Failed to load users', error)
    }
  }

  useEffect(() => {
    loadUsers()
  }, [])

  const handleEdit = (u: User) => {
    setEditingId(u.Id)
    setFullName(u.FullName)
    setUsername(u.Username)
    setPassword('')
    setExistingHash(u.PasswordHash)
    setRole(u.Role)
    setIsActive(u.IsActive === true || u.IsActive === 1)
  }

  const handleClear = () => {
    setEditingId(null)
    setFullName('')
    setUsername('')
    setPassword('')
    setExistingHash('')
    setRole(2)
    setIsActive(true)
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()

    const safeFullName = fullName.trim()
    const safeUsername = username.trim().toLowerCase()

    if (!safeFullName || !safeUsername)
      return Swal.fire('Missing Info', 'Name and Username are required!', 'warning')
    if (!editingId && !password)
      return Swal.fire('Missing Info', 'Password is required for new users!', 'warning')

    let finalHash = existingHash
    if (password) finalHash = await hashPassword(password)

    const finalPerms = role === 1 ? 'ALL' : STAFF_PERMISSIONS
    const payload = {
      Id: editingId,
      Username: safeUsername,
      PasswordHash: finalHash,
      FullName: safeFullName,
      Role: role,
      IsActive: isActive,
      Permissions: finalPerms
    }

    try {
      if (editingId) {
        // @ts-ignore
        await window.api.updateUser(payload)
        Swal.fire({ title: 'Updated!', icon: 'success', timer: 1500 })
      } else {
        const isDuplicate = users.some((u) => u.Username.toLowerCase() === safeUsername)
        if (isDuplicate)
          return Swal.fire('Duplicate', `Username '${safeUsername}' is taken.`, 'error')
        // @ts-ignore
        await window.api.addUser(payload)
        Swal.fire({ title: 'Created!', icon: 'success', timer: 1500 })
      }
      handleClear()
      loadUsers()
    } catch (err: any) {
      Swal.fire('Error', `Error saving user: ${err.message}`, 'error')
    }
  }

  const handleToggleBlock = async (u: User, currentStatus: boolean) => {
    if (u.Role === 0 || u.Id === currentUser?.Id) {
      return Swal.fire(
        'Security Warning',
        'You cannot block yourself or the Master Root account.',
        'error'
      )
    }

    const confirmResult = await Swal.fire({
      title: `${currentStatus ? 'BLOCK' : 'UNBLOCK'} USER?`,
      text: `Are you sure you want to ${currentStatus ? 'block' : 'unblock'} ${u.Username}?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: currentStatus ? '#dc2626' : '#16a34a',
      confirmButtonText: `Yes, ${currentStatus ? 'Block' : 'Unblock'}`
    })

    if (confirmResult.isConfirmed) {
      try {
        const payload = { ...u, IsActive: !currentStatus }
        // @ts-ignore
        await window.api.updateUser(payload)
        loadUsers()
        if (editingId === u.Id) handleClear()
        Swal.fire(
          'Success',
          `User ${currentStatus ? 'blocked' : 'unblocked'} successfully.`,
          'success'
        )
      } catch (err: any) {
        Swal.fire('Error', `Error updating status: ${err.message}`, 'error')
      }
    }
  }

  const filteredUsers = users.filter(
    (u) =>
      u.FullName.toLowerCase().includes(search.toLowerCase()) ||
      u.Username.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className={styles.container}>
      <div className={styles.leftPanel}>
        <div className={styles.panelHeader}>
          <h2 className={styles.panelTitle}>SYSTEM ACCOUNTS</h2>
          <input
            type="text"
            className="pos-input"
            style={{ width: '300px' }}
            placeholder="Search by name or username..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>USERNAME</th>
                <th>FULL NAME</th>
                <th>ROLE</th>
                <th>STATUS</th>
                <th style={{ textAlign: 'right' }}>COMMAND</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.length === 0 ? (
                <tr>
                  <td
                    colSpan={5}
                    style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}
                  >
                    No users found.
                  </td>
                </tr>
              ) : (
                filteredUsers.map((u) => {
                  const active = u.IsActive === true || u.IsActive === 1
                  return (
                    <tr
                      key={u.Id}
                      onClick={() => handleEdit(u)}
                      style={{
                        cursor: 'pointer',
                        backgroundColor: editingId === u.Id ? '#eff6ff' : 'transparent'
                      }}
                    >
                      <td
                        style={{
                          fontWeight: 800,
                          color: 'var(--primary)',
                          fontFamily: 'monospace'
                        }}
                      >
                        {u.Username}
                      </td>
                      <td style={{ fontWeight: 800 }}>{u.FullName}</td>
                      <td>
                        <span
                          className={styles.statusBadge}
                          style={{
                            backgroundColor: u.Role === 1 ? '#fef3c7' : '#f1f5f9',
                            color: u.Role === 1 ? '#b45309' : 'var(--text-muted)'
                          }}
                        >
                          {u.Role === 1 ? 'ADMIN' : 'STAFF'}
                        </span>
                      </td>
                      <td>
                        <span
                          className={styles.statusBadge}
                          style={{
                            backgroundColor: active ? '#dcfce7' : '#fee2e2',
                            color: active ? '#16a34a' : '#dc2626'
                          }}
                        >
                          {active ? 'ACTIVE' : 'BLOCKED'}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button
                          className={`pos-btn ${active ? 'danger' : 'success'}`}
                          style={{ minHeight: '35px', padding: '5px 15px', fontSize: '11px' }}
                          onClick={(e) => {
                            e.stopPropagation()
                            handleToggleBlock(u, active)
                          }}
                        >
                          {active ? 'BLOCK' : 'UNBLOCK'}
                        </button>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className={styles.rightPanel}>
        <div className={styles.formHeader}>
          <h2
            className={styles.panelTitle}
            style={{ color: editingId ? 'var(--action-warning)' : 'var(--text-main)' }}
          >
            {editingId ? 'MODIFY ACCOUNT' : 'REGISTER NEW USER'}
          </h2>
        </div>

        <form onSubmit={handleSave} className={styles.formBody}>
          <div className={styles.formGroup}>
            <label>FULL NAME *</label>
            <input
              type="text"
              className="pos-input"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="e.g. John Doe"
              required
            />
          </div>

          <div className={styles.formGroup}>
            <label>USERNAME *</label>
            <input
              type="text"
              className="pos-input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="login_id"
              required
              disabled={!!editingId}
            />
          </div>

          <div className={styles.formGroup}>
            <label>SYSTEM ROLE</label>
            <select
              className="pos-input"
              value={role}
              onChange={(e) => setRole(Number(e.target.value))}
            >
              <option value={2}>Staff (Fixed Permissions)</option>
              <option value={1}>Administrator (Full Access)</option>
            </select>
          </div>

          <div className={styles.formGroup}>
            <label>
              PASSWORD{' '}
              {editingId && (
                <span style={{ textTransform: 'none', fontWeight: 'normal' }}>
                  (Leave blank to keep current)
                </span>
              )}
            </label>
            <input
              type="password"
              className="pos-input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
            />
          </div>

          <div className={styles.infoBox}>
            <div style={{ fontSize: '12px', fontWeight: 900, marginBottom: '5px' }}>
              PERMISSIONS NOTE:
            </div>
            <p style={{ margin: 0, fontSize: '12px', lineHeight: 1.4 }}>
              {role === 1
                ? 'Administrators have unrestricted access to all modules.'
                : 'Staff are restricted to POS, Returns, View Products, Alerts, and Ledger.'}
            </p>
          </div>

          <div style={{ display: 'flex', gap: '15px', marginTop: 'auto' }}>
            <button
              type="button"
              className="pos-btn neutral"
              style={{ flex: 1 }}
              onClick={handleClear}
            >
              {editingId ? 'CANCEL' : 'CLEAR'}
            </button>
            <button
              type="submit"
              className={`pos-btn ${editingId ? 'warning' : 'success'}`}
              style={{ flex: 2 }}
            >
              {editingId ? 'UPDATE ACCOUNT' : 'CREATE ACCOUNT'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
