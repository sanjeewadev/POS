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
      {/* ── Left: User Table ── */}
      <div className={styles.leftPanel}>
        <div className={styles.panelHeader}>
          {/* 🚀 Using Global Title Class */}
          <h2 className="pos-page-title">System Accounts</h2>
          <input
            type="text"
            className={`pos-input ${styles.searchInput}`}
            placeholder="Search users by name or username..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>Username</th>
                <th>Full Name</th>
                <th>Role</th>
                <th>Status</th>
                <th className={styles.actionHead}>Action</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={5} className={styles.emptyState}>
                    No users found. Try a different search term.
                  </td>
                </tr>
              ) : (
                filteredUsers.map((u) => {
                  const active = u.IsActive === true || u.IsActive === 1
                  return (
                    <tr
                      key={u.Id}
                      onClick={() => handleEdit(u)}
                      className={editingId === u.Id ? styles.selectedRow : ''}
                    >
                      <td className={styles.usernameCell}>{u.Username}</td>
                      <td className={styles.nameCell}>{u.FullName}</td>
                      <td>
                        <span
                          className={`${styles.statusBadge} ${u.Role === 1 ? styles.badgeAdmin : styles.badgeStaff}`}
                        >
                          {u.Role === 1 ? 'Administrator' : 'Staff'}
                        </span>
                      </td>
                      <td>
                        <span
                          className={`${styles.statusBadge} ${active ? styles.badgeActive : styles.badgeBlocked}`}
                        >
                          {active ? 'Active' : 'Blocked'}
                        </span>
                      </td>
                      <td className={styles.actionCell}>
                        <button
                          className={`pos-btn ${active ? 'danger' : 'success'} ${styles.actionBtn}`}
                          onClick={(e) => {
                            e.stopPropagation()
                            handleToggleBlock(u, active)
                          }}
                        >
                          {active ? 'Block' : 'Unblock'}
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

      {/* ── Right: Form ── */}
      <div className={styles.rightPanel}>
        <div className={styles.formHeader}>
          {/* 🚀 Using Global Title Class */}
          <h2
            className={`pos-page-title ${editingId ? styles.formTitleEdit : styles.formTitleNew}`}
          >
            {editingId ? 'Modify Account' : 'Create New Account'}
          </h2>
        </div>

        <form onSubmit={handleSave} className={styles.formBody}>
          <div className={styles.formGroup}>
            <label>
              Full Name <span className={styles.required}>*</span>
            </label>
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
            <label>
              Username <span className={styles.required}>*</span>
            </label>
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
            <label>System Role</label>
            <select
              className="pos-input"
              value={role}
              onChange={(e) => setRole(Number(e.target.value))}
            >
              <option value={2}>Staff — Limited permissions</option>
              <option value={1}>Administrator — Full access</option>
            </select>
          </div>

          <div className={styles.formGroup}>
            <label>
              Password{' '}
              {editingId && (
                <span className={styles.optional}>(leave blank to keep current password)</span>
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
            <div className={styles.infoTitle}>Permissions</div>
            <p className={styles.infoText}>
              {role === 1
                ? 'Administrators can access every module in the system.'
                : 'Staff accounts are restricted to POS operations, returns, product view, alerts, sales reports, and credit ledger.'}
            </p>
          </div>

          <div className={styles.formActions}>
            <button
              type="button"
              className={`pos-btn neutral ${styles.formBtnCancel}`}
              onClick={handleClear}
            >
              {editingId ? 'Cancel' : 'Clear Form'}
            </button>
            <button
              type="submit"
              className={`pos-btn ${editingId ? 'warning' : 'success'} ${styles.formBtnSubmit}`}
            >
              {editingId ? 'Update Account' : 'Create Account'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
