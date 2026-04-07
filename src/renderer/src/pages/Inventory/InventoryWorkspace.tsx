// src/renderer/src/pages/Inventory/InventoryWorkspace.tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import { RiSearchLine, RiScales3Line, RiFoldersLine } from 'react-icons/ri'
import styles from './InventoryWorkspace.module.css'

import ProductCatalog from './ProductCatalog'
import CatalogManager from './CatalogManager'
import AdjustStock from './AdjustStock'

export default function InventoryWorkspace() {
  const { currentUser } = useAuth()
  const [activeTab, setActiveTab] = useState('Products')

  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1

  useEffect(() => {
    if (!isAdmin && activeTab === 'Catalog') {
      setActiveTab('Products')
    }
  }, [activeTab, isAdmin])

  const renderContent = () => {
    switch (activeTab) {
      case 'Products':
        return <ProductCatalog />
      case 'Adjustments':
        return <AdjustStock />
      case 'Catalog':
        return isAdmin ? <CatalogManager /> : null
      default:
        return null
    }
  }

  return (
    <div className={styles.workspaceContainer}>
      <aside className={styles.innerSidebar}>
        <div className={styles.navGroup}>
          <div className={styles.groupLabel}>Inventory</div>

          <button
            className={`${styles.navBtn} ${activeTab === 'Products' ? styles.active : ''}`}
            onClick={() => setActiveTab('Products')}
          >
            <RiSearchLine className={styles.navIcon} />
            <div className={styles.navText}>
              <strong>View Products</strong>
              <span>Quick Stock Add</span>
            </div>
          </button>

          {/* 🚀 REMOVED: The Receive GRN Button was here. Removed for V1.0 Simplicity. */}

          <button
            className={`${styles.navBtn} ${activeTab === 'Adjustments' ? styles.active : ''}`}
            onClick={() => setActiveTab('Adjustments')}
          >
            <RiScales3Line className={styles.navIcon} />
            <div className={styles.navText}>
              <strong>Adjust Stock</strong>
              <span>Loss / Corrections</span>
            </div>
          </button>
          {isAdmin && (
            <button
              className={`${styles.navBtn} ${activeTab === 'Catalog' ? styles.active : ''}`}
              onClick={() => setActiveTab('Catalog')}
            >
              <RiFoldersLine className={styles.navIcon} />
              <div className={styles.navText}>
                <strong>Catalog Setup</strong>
                <span>Folders & Items</span>
              </div>
            </button>
          )}
        </div>
      </aside>

      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
