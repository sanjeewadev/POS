// src/renderer/src/pages/Settings/SettingsWorkspace.tsx
import { useState } from 'react'
import styles from './SettingsWorkspace.module.css'
import { RiStore2Line, RiTeamLine, RiDatabase2Line, RiTabletLine } from 'react-icons/ri' // 🚀 Added Tablet Icon

import UserManager from './UserManager'
import SystemBackups from './SystemBackups'
import ShopSettings from './ShopSettings'
import DeviceSettings from './DeviceSettings' // 🚀 The new page we are about to build!

const NAV_ITEMS = [
  {
    group: 'Store Configuration',
    items: [
      {
        key: 'Shop',
        label: 'Shop Details',
        sub: 'Name, Receipt, Address',
        icon: <RiStore2Line size={18} />
      },
      // 🚀 NEW: Device & UI Settings Tab
      {
        key: 'Device',
        label: 'Device & UI',
        sub: 'Touch Numpad & Layout',
        icon: <RiTabletLine size={18} />
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
      case 'Device': // 🚀 New Route
        return <DeviceSettings />
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
