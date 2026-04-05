// src/renderer/src/pages/Inventory/InventoryWorkspace.tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import {
  RiSearchLine,
  RiTruckLine,
  RiScales3Line,
  RiFoldersLine,
  RiBuilding2Line
} from 'react-icons/ri'
import styles from './InventoryWorkspace.module.css'

import ProductCatalog from './ProductCatalog'
import SupplierManager from './SupplierManager'
import StockInManager from './StockInManager'
import CatalogManager from './CatalogManager'
import AdjustStock from './AdjustStock'

export default function InventoryWorkspace() {
  const { currentUser } = useAuth()
  const [activeTab, setActiveTab] = useState('Products')

  const isAdmin = currentUser?.Role === 0 || currentUser?.Role === 1

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

          <button
            className={`${styles.navBtn} ${activeTab === 'StockIn' ? styles.active : ''}`}
            onClick={() => setActiveTab('StockIn')}
          >
            <RiTruckLine className={styles.navIcon} />
            <div className={styles.navText}>
              <strong>Receive GRN</strong>
              <span>Supplier Invoices</span>
            </div>
          </button>

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
        </div>

        {isAdmin && (
          <div className={styles.navGroup}>
            <div className={styles.groupLabel} style={{ color: 'var(--action-warning)' }}>
              Manager Tools
            </div>

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

            <button
              className={`${styles.navBtn} ${activeTab === 'Suppliers' ? styles.active : ''}`}
              onClick={() => setActiveTab('Suppliers')}
            >
              <RiBuilding2Line className={styles.navIcon} />
              <div className={styles.navText}>
                <strong>Suppliers</strong>
                <span>Vendor Profiles</span>
              </div>
            </button>
          </div>
        )}
      </aside>

      <section className={styles.contentArea}>{renderContent()}</section>
    </div>
  )
}
