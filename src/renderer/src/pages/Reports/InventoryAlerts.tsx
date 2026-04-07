// src/renderer/src/pages/Reports/InventoryAlerts.tsx
import { useState, useEffect } from 'react'
import { Product } from '../../types/models'
import {
  RiAlertLine,
  RiErrorWarningLine,
  RiCheckboxCircleLine,
  RiRefreshLine
} from 'react-icons/ri'
import styles from './InventoryAlerts.module.css'

export default function InventoryAlerts() {
  const [alertItems, setAlertItems] = useState<Product[]>([])
  const [loading, setLoading] = useState(false)

  const loadData = async () => {
    setLoading(true)
    try {
      // @ts-ignore
      const prodData = await window.api.getLowStockAlerts(10)
      setAlertItems(prodData || [])
    } catch (error) {
      console.error('Failed to load products for alerts', error)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  const outOfStockItems = alertItems.filter((p) => p.Quantity <= 0)
  const lowStockItems = alertItems.filter((p) => p.Quantity > 0)

  const formatQty = (qty: number) => {
    return qty % 1 !== 0 ? qty.toFixed(2) : qty
  }

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          {/* 🚀 Applied Global Title */}
          <h2 className="pos-page-title">Inventory Alerts Dashboard</h2>
          <button
            className={`pos-btn neutral ${styles.refreshBtn}`}
            onClick={loadData}
            disabled={loading}
          >
            <RiRefreshLine className={loading ? styles.spin : ''} />
            {loading ? 'REFRESHING...' : 'REFRESH ALERTS'}
          </button>
        </div>

        <div className={styles.panelBody}>
          <div className={styles.gridContainer}>
            {/* --- LEFT PANEL: OUT OF STOCK --- */}
            <div className={styles.alertColumn}>
              <div className={`${styles.columnHeader} ${styles.dangerHeader}`}>
                <RiErrorWarningLine size={20} />
                <span>OUT OF STOCK ({outOfStockItems.length})</span>
              </div>

              <div className={styles.listWrapper}>
                {outOfStockItems.length === 0 ? (
                  <div className={styles.emptyState}>
                    <RiCheckboxCircleLine size={48} className={styles.emptyIconSuccess} />
                    <h3>Looking Good!</h3>
                    <p>No active products are out of stock.</p>
                  </div>
                ) : (
                  <div className={styles.cardGrid}>
                    {outOfStockItems.map((p) => (
                      <div key={p.Id} className={`${styles.alertCard} ${styles.dangerCard}`}>
                        <div className={styles.itemInfo}>
                          <span className={styles.itemName}>{p.Name}</span>
                          <span className={styles.itemCode}>CODE: {p.Barcode || 'N/A'}</span>
                        </div>
                        <div className={`${styles.statusBadge} ${styles.dangerBadge}`}>EMPTY</div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* --- RIGHT PANEL: LOW STOCK --- */}
            <div className={styles.alertColumn}>
              <div className={`${styles.columnHeader} ${styles.warningHeader}`}>
                <RiAlertLine size={20} />
                <span>LOW STOCK WARNINGS ({lowStockItems.length})</span>
              </div>

              <div className={styles.listWrapper}>
                {lowStockItems.length === 0 ? (
                  <div className={styles.emptyState}>
                    <RiCheckboxCircleLine size={48} className={styles.emptyIconSuccess} />
                    <h3>Well Stocked!</h3>
                    <p>No products are currently running low.</p>
                  </div>
                ) : (
                  <div className={styles.cardGrid}>
                    {lowStockItems.map((p) => (
                      <div key={p.Id} className={`${styles.alertCard} ${styles.warningCard}`}>
                        <div className={styles.itemInfo}>
                          <span className={styles.itemName}>{p.Name}</span>
                          <span className={styles.itemCode}>CODE: {p.Barcode || 'N/A'}</span>
                        </div>
                        <div className={`${styles.statusBadge} ${styles.warningBadge}`}>
                          {formatQty(p.Quantity)} {p.Unit} LEFT
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
