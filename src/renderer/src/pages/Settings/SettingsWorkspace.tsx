// src/renderer/src/pages/Settings/SettingsWorkspace.tsx
import { useState } from 'react'
import styles from './SettingsWorkspace.module.css'
import { RiStore2Line, RiTeamLine, RiDatabase2Line } from 'react-icons/ri'

import UserManager from './UserManager'
import SystemBackups from './SystemBackups'
import ShopSettings from './ShopSettings'

const NAV_ITEMS = [
  {
    group: 'Store Configuration',
    items: [
      {
        key: 'Shop',
        label: 'Shop Details',
        sub: 'Name, Receipt, Address',
        icon: <RiStore2Line size={18} />
      }
    ]
  },
  {
    group: 'Maintenance',
    items: [
      {
        key: 'System',
        label: 'System & DB',
        sub: 'Backups & Resets',
        icon: <RiDatabase2Line size={18} />
      }
    ]
  },
  {
    group: 'Security & Access',
    items: [
      {
        key: 'Users',
        label: 'User Accounts',
        sub: 'Cashiers & Admins',
        icon: <RiTeamLine size={18} />
      }
    ]
  }
]

export default function SettingsWorkspace() {
  const [activeTab, setActiveTab] = useState('Shop')

  const renderContent = () => {
    switch (activeTab) {
      case 'Users':
        return <UserManager />
      case 'Shop':
        return <ShopSettings />

      case 'System':
        return <SystemBackups />
      default:
        return null
    }
  }

  return (
    <div className={styles.workspaceContainer}>
      <aside className={styles.innerSidebar}>
        {NAV_ITEMS.map((group) => (
          <div key={group.group} className={styles.navGroup}>
            <div className={styles.groupLabel}>{group.group}</div>
            {group.items.map((item) => (
              <button
                key={item.key}
                className={`${styles.navBtn} ${activeTab === item.key ? styles.active : ''}`}
                onClick={() => setActiveTab(item.key)}
              >
                <span className={styles.navIcon}>{item.icon}</span>
                <div className={styles.navText}>
                  <strong>{item.label}</strong>
                  <span>{item.sub}</span>
                </div>
              </button>
            ))}
          </div>
        ))}
      </aside>

      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
