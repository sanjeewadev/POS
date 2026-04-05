// src/renderer/src/pages/Settings/SettingsWorkspace.tsx
import { useState } from 'react'
import styles from './SettingsWorkspace.module.css'

import UserManager from './UserManager'
import SystemBackups from './SystemBackups'
import ShopSettings from './ShopSettings' // 🚀 NEW: We will build this next!

export default function SettingsWorkspace() {
  // Default to the new Shop Settings page
  const [activeTab, setActiveTab] = useState('Shop')

  const renderContent = () => {
    switch (activeTab) {
      case 'Shop':
        return <ShopSettings />
      case 'Users':
        return <UserManager />
      case 'System':
        return <SystemBackups />
      default:
        return null
    }
  }

  return (
    <div className={styles.workspaceContainer}>
      {/* --- INNER SIDEBAR MENU --- */}
      <aside className={styles.innerSidebar}>
        <div className={styles.menuHeader}>STORE CONFIGURATION</div>

        <button
          className={`${styles.navBtn} ${activeTab === 'Shop' ? styles.active : ''}`}
          onClick={() => setActiveTab('Shop')}
        >
          <span className={styles.icon}>🏪</span>
          <div className={styles.btnText}>
            <strong>Shop Details</strong>
            <span>Name, Receipt, Address</span>
          </div>
        </button>

        <div className={styles.divider}></div>
        <div className={styles.menuHeader}>SECURITY & ACCESS</div>

        <button
          className={`${styles.navBtn} ${activeTab === 'Users' ? styles.active : ''}`}
          onClick={() => setActiveTab('Users')}
        >
          <span className={styles.icon}>👥</span>
          <div className={styles.btnText}>
            <strong>User Accounts</strong>
            <span>Cashiers & Admins</span>
          </div>
        </button>

        <div className={styles.divider}></div>
        <div className={styles.menuHeader}>MAINTENANCE</div>

        <button
          className={`${styles.navBtn} ${activeTab === 'System' ? styles.active : ''}`}
          onClick={() => setActiveTab('System')}
        >
          <span className={styles.icon}>⚙️</span>
          <div className={styles.btnText}>
            <strong>System & DB</strong>
            <span>Backups & Resets</span>
          </div>
        </button>
      </aside>

      {/* --- MAIN CONTENT AREA --- */}
      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
