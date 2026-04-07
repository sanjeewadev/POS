// src/renderer/src/pages/Reports/ReportsWorkspace.tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import {
  RiBarChartBoxLine,
  RiAlarmWarningLine,
  RiHistoryLine,
  RiShieldUserLine
} from 'react-icons/ri'
import styles from './ReportsWorkspace.module.css'

import Dashboard from './Dashboard'
import InventoryAlerts from './InventoryAlerts'
import SalesHistory from './SalesHistory'
import AuditLogs from './AuditLogs'

export default function ReportsWorkspace() {
  const { currentUser } = useAuth()
  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1
  const [activeTab, setActiveTab] = useState(isAdmin ? 'Dashboard' : 'Alerts')

  useEffect(() => {
    if (!isAdmin && (activeTab === 'Dashboard' || activeTab === 'Audit')) {
      setActiveTab('Alerts')
    }
  }, [activeTab, isAdmin])

  const renderContent = () => {
    switch (activeTab) {
      case 'Alerts':
        return <InventoryAlerts />
      case 'SalesHistory':
        return <SalesHistory />
      case 'Dashboard':
        return isAdmin ? <Dashboard /> : null
      case 'Audit':
        return isAdmin ? <AuditLogs /> : null
      default:
        return null
    }
  }

  return (
    <div className={styles.workspaceContainer}>
      <aside className={styles.innerSidebar}>
        {/* 🚀 ONE SINGLE GROUP FOR EVERYTHING */}
        <div className={styles.navGroup}>
          <div className={styles.groupLabel}>Reports</div>

          {isAdmin && (
            <button
              className={`${styles.navBtn} ${activeTab === 'Dashboard' ? styles.active : ''}`}
              onClick={() => setActiveTab('Dashboard')}
            >
              <RiBarChartBoxLine className={styles.navIcon} />
              <div className={styles.navText}>
                <strong>Dashboard</strong>
                <span>Revenue & Profit</span>
              </div>
            </button>
          )}

          <button
            className={`${styles.navBtn} ${activeTab === 'Alerts' ? styles.active : ''}`}
            onClick={() => setActiveTab('Alerts')}
          >
            <RiAlarmWarningLine className={styles.navIcon} />
            <div className={styles.navText}>
              <strong>Stock Alerts</strong>
              <span>Low Inventory</span>
            </div>
          </button>

          <button
            className={`${styles.navBtn} ${activeTab === 'SalesHistory' ? styles.active : ''}`}
            onClick={() => setActiveTab('SalesHistory')}
          >
            <RiHistoryLine className={styles.navIcon} />
            <div className={styles.navText}>
              <strong>Sales History</strong>
              <span>Past Receipts</span>
            </div>
          </button>

          {isAdmin && (
            <button
              className={`${styles.navBtn} ${activeTab === 'Audit' ? styles.active : ''}`}
              onClick={() => setActiveTab('Audit')}
            >
              <RiShieldUserLine className={styles.navIcon} />
              <div className={styles.navText}>
                <strong>Audit Logs</strong>
                <span>Security & Voids</span>
              </div>
            </button>
          )}
        </div>
      </aside>

      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
