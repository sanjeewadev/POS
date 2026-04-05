// src/renderer/src/pages/Reports/ReportsWorkspace.tsx
import { useState, useEffect } from 'react'
import styles from './ReportsWorkspace.module.css' // 🚀 We will create a specific CSS file for this
import { useAuth } from '../../store/AuthContext'

import Dashboard from './Dashboard'
import InventoryAlerts from './InventoryAlerts'
import TodaySales from './TodaySales'
import SalesHistory from './SalesHistory'
import CreditAccounts from './CreditAccounts'
import AuditLogs from './AuditLogs'

export default function ReportsWorkspace() {
  const { currentUser } = useAuth()

  // RBAC: Recognize both Root and Admin
  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1

  // Staff defaults to Alerts, Admin defaults to Dashboard
  const [activeTab, setActiveTab] = useState(isAdmin ? 'Dashboard' : 'Alerts')

  // RBAC: Block staff from Dashboard and Audit
  useEffect(() => {
    if (!isAdmin && (activeTab === 'Dashboard' || activeTab === 'Audit')) {
      setActiveTab('Alerts')
    }
  }, [activeTab, isAdmin])

  const renderContent = () => {
    switch (activeTab) {
      case 'Alerts':
        return <InventoryAlerts />
      case 'TodaySales':
        return <TodaySales />
      case 'SalesHistory':
        return <SalesHistory />
      case 'Credit':
        return <CreditAccounts />
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
      {/* --- INNER SIDEBAR MENU --- */}
      <aside className={styles.innerSidebar}>
        {isAdmin && (
          <>
            <div className={styles.menuHeader}>EXECUTIVE</div>
            <button
              className={`${styles.navBtn} ${activeTab === 'Dashboard' ? styles.active : ''}`}
              onClick={() => setActiveTab('Dashboard')}
            >
              <span className={styles.icon}>📊</span>
              <div className={styles.btnText}>
                <strong>Dashboard</strong>
                <span>Revenue & Profit</span>
              </div>
            </button>
            <div className={styles.divider}></div>
          </>
        )}

        <div className={styles.menuHeader}>DAILY OPERATIONS</div>
        <button
          className={`${styles.navBtn} ${activeTab === 'Alerts' ? styles.active : ''}`}
          onClick={() => setActiveTab('Alerts')}
        >
          <span className={styles.icon}>🚨</span>
          <div className={styles.btnText}>
            <strong>Stock Alerts</strong>
            <span>Low Inventory</span>
          </div>
        </button>
        <button
          className={`${styles.navBtn} ${activeTab === 'TodaySales' ? styles.active : ''}`}
          onClick={() => setActiveTab('TodaySales')}
        >
          <span className={styles.icon}>💰</span>
          <div className={styles.btnText}>
            <strong>Today's Sales</strong>
            <span>Current Ledger</span>
          </div>
        </button>

        <div className={styles.divider}></div>
        <div className={styles.menuHeader}>FINANCIALS</div>
        <button
          className={`${styles.navBtn} ${activeTab === 'SalesHistory' ? styles.active : ''}`}
          onClick={() => setActiveTab('SalesHistory')}
        >
          <span className={styles.icon}>📅</span>
          <div className={styles.btnText}>
            <strong>Sales History</strong>
            <span>Past Receipts</span>
          </div>
        </button>
        <button
          className={`${styles.navBtn} ${activeTab === 'Credit' ? styles.active : ''}`}
          onClick={() => setActiveTab('Credit')}
        >
          <span className={styles.icon}>💳</span>
          <div className={styles.btnText}>
            <strong>Credit Accounts</strong>
            <span>Debt Collection</span>
          </div>
        </button>

        {isAdmin && (
          <>
            <div className={styles.divider}></div>
            <div className={styles.menuHeader} style={{ color: 'var(--action-danger)' }}>
              SYSTEM
            </div>
            <button
              className={`${styles.navBtn} ${activeTab === 'Audit' ? styles.active : ''}`}
              onClick={() => setActiveTab('Audit')}
            >
              <span className={styles.icon}>🛡️</span>
              <div className={styles.btnText}>
                <strong>Audit Logs</strong>
                <span>Security & Voids</span>
              </div>
            </button>
          </>
        )}
      </aside>

      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
