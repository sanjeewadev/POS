// src/renderer/src/pages/Inventory/InventoryWorkspace.tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import styles from './InventoryWorkspace.module.css'

// We will update these individual pages soon!
import ProductCatalog from './ProductCatalog'
import SupplierManager from './SupplierManager'
import StockInManager from './StockInManager'
import CatalogManager from './CatalogManager'
import AdjustStock from './AdjustStock'

export default function InventoryWorkspace() {
  const { currentUser } = useAuth()
  const [activeTab, setActiveTab] = useState('Products')

  // 🚀 RBAC: Recognize both Root (0) and Admin (1)
  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1

  // 🚀 RBAC: If a staff member tries to navigate to a blocked tab, kick them back
  useEffect(() => {
    if (!isAdmin && (activeTab === 'Catalog' || activeTab === 'Suppliers')) {
      setActiveTab('Products')
    }
  }, [activeTab, isAdmin])

  const renderContent = () => {
    switch (activeTab) {
      case 'Products':
        return <ProductCatalog />
      case 'StockIn':
        return <StockInManager />
      case 'Adjustments':
        return <AdjustStock />
      case 'Catalog':
        return isAdmin ? <CatalogManager /> : null
      case 'Suppliers':
        return isAdmin ? <SupplierManager /> : null
      default:
        return null
    }
  }

  return (
    <div className={styles.workspaceContainer}>
      {/* --- INNER SIDEBAR MENU --- */}
      <aside className={styles.innerSidebar}>
        <div className={styles.menuHeader}>INVENTORY</div>

        <button
          className={`${styles.navBtn} ${activeTab === 'Products' ? styles.active : ''}`}
          onClick={() => setActiveTab('Products')}
        >
          <span className={styles.icon}>🔍</span>
          <div className={styles.btnText}>
            <strong>View Products</strong>
            <span>Quick Stock Add</span>
          </div>
        </button>

        <button
          className={`${styles.navBtn} ${activeTab === 'StockIn' ? styles.active : ''}`}
          onClick={() => setActiveTab('StockIn')}
        >
          <span className={styles.icon}>🚚</span>
          <div className={styles.btnText}>
            <strong>Receive GRN</strong>
            <span>Supplier Invoices</span>
          </div>
        </button>

        <button
          className={`${styles.navBtn} ${activeTab === 'Adjustments' ? styles.active : ''}`}
          onClick={() => setActiveTab('Adjustments')}
        >
          <span className={styles.icon}>⚖️</span>
          <div className={styles.btnText}>
            <strong>Adjust Stock</strong>
            <span>Loss / Corrections</span>
          </div>
        </button>

        {/* 🚀 RBAC: Hide Management Tools from standard Staff */}
        {isAdmin && (
          <>
            <div className={styles.divider}></div>
            <div className={styles.menuHeader} style={{ color: 'var(--action-warning)' }}>
              MANAGER TOOLS
            </div>

            <button
              className={`${styles.navBtn} ${activeTab === 'Catalog' ? styles.active : ''}`}
              onClick={() => setActiveTab('Catalog')}
            >
              <span className={styles.icon}>🗂️</span>
              <div className={styles.btnText}>
                <strong>Catalog Setup</strong>
                <span>Folders & Items</span>
              </div>
            </button>

            <button
              className={`${styles.navBtn} ${activeTab === 'Suppliers' ? styles.active : ''}`}
              onClick={() => setActiveTab('Suppliers')}
            >
              <span className={styles.icon}>🏢</span>
              <div className={styles.btnText}>
                <strong>Suppliers</strong>
                <span>Vendor Profiles</span>
              </div>
            </button>
          </>
        )}
      </aside>

      {/* --- MAIN CONTENT AREA --- */}
      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
