// src/renderer/src/pages/Reports/TodaySales.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import {
  RiMoneyDollarCircleLine,
  RiCashLine,
  RiBankCardLine,
  RiCloseCircleLine,
  RiSearchLine,
  RiRefreshLine,
  RiPrinterLine,
  RiCloseLine,
  RiInformationLine
} from 'react-icons/ri'
import styles from './TodaySales.module.css'

export default function TodaySales() {
  const [sales, setSales] = useState<any[]>([])
  const [loading, setLoading] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')

  // Modal for viewing receipt items
  const [viewingReceipt, setViewingReceipt] = useState<any | null>(null)
  const [receiptItems, setReceiptItems] = useState<any[]>([])

  const loadTodaySales = async () => {
    setLoading(true)
    try {
      // @ts-ignore
      const data = await window.api.getTodaySales()
      setSales(data || [])
    } catch (err: any) {
      console.error('Failed to load sales', err)
      Swal.fire('Error', 'Failed to load sales: ' + err.message, 'error')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadTodaySales()
  }, [])

  const handleViewItems = async (txn: any) => {
    setViewingReceipt(txn)
    try {
      // @ts-ignore
      const items = await window.api.getReceiptItems(txn.ReceiptId)
      setReceiptItems(items || [])
    } catch (err: any) {
      Swal.fire('Error', 'Failed to load receipt details: ' + err.message, 'error')
      setReceiptItems([])
    }
  }

  const handleVoid = async (receiptId: string) => {
    const confirmResult = await Swal.fire({
      title: '🚨 DANGER: VOID RECEIPT',
      text: `Are you sure you want to VOID receipt ${receiptId}?\n\nThis will return all items to stock and cancel the sale permanently.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, VOID IT!'
    })

    if (confirmResult.isConfirmed) {
      try {
        // @ts-ignore
        await window.api.voidReceipt(receiptId)
        Swal.fire('Success', '✅ Receipt voided successfully.', 'success')
        loadTodaySales()
      } catch (err: any) {
        Swal.fire('Error', err.message || 'Error voiding receipt.', 'error')
      }
    }
  }

  const renderStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <span className={`${styles.statusBadge} ${styles.statusPaid}`}>PAID</span>
      case 1:
        return <span className={`${styles.statusBadge} ${styles.statusUnpaid}`}>UNPAID</span>
      case 2:
        return <span className={`${styles.statusBadge} ${styles.statusPartial}`}>PARTIAL</span>
      case 3:
        return <span className={`${styles.statusBadge} ${styles.statusVoid}`}>VOIDED</span>
      default:
        return <span className={`${styles.statusBadge} ${styles.statusVoid}`}>UNKNOWN</span>
    }
  }

  const totalSavings = receiptItems.reduce((sum, item) => {
    const original = item.UnitCost || item.UnitPrice
    return sum + Math.max(0, original - item.UnitPrice) * item.Quantity
  }, 0)

  const displayedSales = useMemo(() => {
    const q = searchQuery.toLowerCase()
    return sales.filter(
      (s) =>
        s.ReceiptId.toLowerCase().includes(q) ||
        (s.CustomerName && s.CustomerName.toLowerCase().includes(q))
    )
  }, [sales, searchQuery])

  const { totalRevenue, cashSales, creditSales, voidCount } = useMemo(() => {
    let rev = 0,
      cash = 0,
      credit = 0,
      voids = 0
    sales.forEach((s) => {
      if (s.Status === 3) {
        voids++
      } else {
        rev += s.TotalAmount
        if (s.IsCredit) credit += s.TotalAmount
        else cash += s.TotalAmount
      }
    })
    return { totalRevenue: rev, cashSales: cash, creditSales: credit, voidCount: voids }
  }, [sales])

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <h2 className={styles.pageTitle}>Today's Sales Ledger</h2>
        </div>

        <div className={styles.panelBody}>
          {/* Financial Stats Banner */}
          <div className={styles.summaryBanner}>
            <div className={styles.statBox}>
              <RiMoneyDollarCircleLine size={32} color="var(--brand-secondary)" />
              <div className={styles.statContent}>
                <div className={styles.statLabel}>Today's Net Revenue</div>
                <div className={styles.statValueMain}>Rs {totalRevenue.toFixed(2)}</div>
              </div>
            </div>
            <div className={styles.statBox}>
              <RiCashLine size={32} color="var(--action-success)" />
              <div className={styles.statContent}>
                <div className={styles.statLabel}>Cash Sales</div>
                <div className={styles.statValue}>Rs {cashSales.toFixed(2)}</div>
              </div>
            </div>
            <div className={styles.statBox}>
              <RiBankCardLine size={32} color="var(--action-warning)" />
              <div className={styles.statContent}>
                <div className={styles.statLabel}>Credit Sales</div>
                <div className={styles.statValueCredit}>Rs {creditSales.toFixed(2)}</div>
              </div>
            </div>
            <div className={styles.statBox}>
              <RiCloseCircleLine size={32} color="var(--action-danger)" />
              <div className={styles.statContent}>
                <div className={styles.statLabel}>Voids</div>
                <div className={styles.statValueDanger}>{voidCount} Receipts</div>
              </div>
            </div>
          </div>

          <div className={styles.actionHeader}>
            <div className={styles.searchWrapper}>
              <RiSearchLine className={styles.searchIcon} />
              <input
                type="text"
                className="pos-input"
                placeholder="Find Receipt ID or Customer..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <button
              className="pos-btn neutral"
              onClick={loadTodaySales}
              disabled={loading}
              style={{ minHeight: '48px', gap: '8px' }}
            >
              <RiRefreshLine /> {loading ? 'Loading...' : 'Refresh'}
            </button>
          </div>

          <div className={styles.tableWrapper}>
            <table className={styles.classicTable}>
              <thead>
                <tr>
                  <th>TIME</th>
                  <th>RECEIPT ID</th>
                  <th>CUSTOMER</th>
                  <th>PAYMENT</th>
                  <th>TOTAL</th>
                  <th>STATUS</th>
                  <th style={{ textAlign: 'right' }}>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {displayedSales.length === 0 ? (
                  <tr>
                    <td
                      colSpan={7}
                      style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}
                    >
                      No sales found for today.
                    </td>
                  </tr>
                ) : (
                  displayedSales.map((s) => (
                    <tr key={s.ReceiptId} className={s.Status === 3 ? styles.voidedRow : ''}>
                      <td style={{ fontWeight: 700 }}>
                        {new Date(s.TransactionDate).toLocaleTimeString([], {
                          hour: '2-digit',
                          minute: '2-digit'
                        })}
                      </td>
                      <td className={styles.receiptIdCell}>{s.ReceiptId}</td>
                      <td style={{ fontWeight: 600 }}>{s.CustomerName || 'Walk-in'}</td>
                      <td>
                        <span className={s.IsCredit ? styles.creditBadge : styles.cashBadge}>
                          {s.IsCredit ? 'CREDIT' : 'CASH'}
                        </span>
                      </td>
                      <td style={{ fontWeight: 900 }}>Rs {s.TotalAmount.toFixed(2)}</td>
                      <td>{renderStatusBadge(s.Status)}</td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                          <button
                            className="pos-btn neutral"
                            onClick={() => handleViewItems(s)}
                            style={{ minHeight: '36px', padding: '0 12px', fontSize: '12px' }}
                          >
                            VIEW
                          </button>
                          {s.Status !== 3 && (
                            <button
                              className="pos-btn danger"
                              onClick={() => handleVoid(s.ReceiptId)}
                              style={{ minHeight: '36px', padding: '0 12px', fontSize: '12px' }}
                            >
                              VOID
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* --- RECEIPT DETAILS MODAL --- */}
      {viewingReceipt && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '20px', fontWeight: 800 }}>
                  Receipt: {viewingReceipt.ReceiptId}
                </h2>
                <div className={styles.modalSubHeader}>
                  <RiInformationLine /> {new Date(viewingReceipt.TransactionDate).toLocaleString()}{' '}
                  | {viewingReceipt.CustomerName || 'Walk-in'}
                </div>
              </div>
              <button className={styles.closeBtn} onClick={() => setViewingReceipt(null)}>
                <RiCloseLine size={24} />
              </button>
            </div>

            <div className={styles.modalBody}>
              <table className={styles.classicTable}>
                <thead>
                  <tr>
                    <th>PRODUCT</th>
                    <th>QTY</th>
                    <th>RETAIL</th>
                    <th>DISC</th>
                    <th>SOLD</th>
                    <th style={{ textAlign: 'right' }}>TOTAL</th>
                  </tr>
                </thead>
                <tbody>
                  {receiptItems.map((item, idx) => {
                    const original = item.UnitCost || item.UnitPrice
                    const discount = Math.max(0, original - item.UnitPrice)
                    return (
                      <tr key={idx}>
                        <td style={{ fontWeight: 700 }}>{item.ProductName}</td>
                        <td style={{ fontWeight: 700 }}>
                          {item.Quantity} {item.Unit}
                        </td>
                        <td
                          style={{
                            textDecoration: discount > 0 ? 'line-through' : 'none',
                            opacity: 0.6
                          }}
                        >
                          Rs {original.toFixed(2)}
                        </td>
                        <td style={{ color: 'var(--action-warning)', fontWeight: 700 }}>
                          {discount > 0 ? `- Rs ${discount.toFixed(2)}` : '-'}
                        </td>
                        <td style={{ color: 'var(--action-success)', fontWeight: 700 }}>
                          Rs {item.UnitPrice.toFixed(2)}
                        </td>
                        <td style={{ textAlign: 'right', fontWeight: 900 }}>
                          Rs {(item.Quantity * item.UnitPrice).toFixed(2)}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>

              <div className={styles.modalSummaryBox}>
                <div className={styles.summaryLeft}>
                  {totalSavings > 0 && (
                    <div className={styles.savingsTag}>⚡ Saved: Rs {totalSavings.toFixed(2)}</div>
                  )}
                </div>
                <div className={styles.summaryRight}>
                  <div className={styles.summaryLine}>
                    <span>Total:</span> <span>Rs {viewingReceipt.TotalAmount.toFixed(2)}</span>
                  </div>
                  <div className={`${styles.summaryLine} ${styles.paidLine}`}>
                    <span>Paid:</span> <span>Rs {viewingReceipt.PaidAmount.toFixed(2)}</span>
                  </div>
                  {viewingReceipt.IsCredit === 1 &&
                    viewingReceipt.TotalAmount - viewingReceipt.PaidAmount > 0 && (
                      <div className={`${styles.summaryLine} ${styles.balanceLine}`}>
                        <span>Due:</span>{' '}
                        <span>
                          Rs {(viewingReceipt.TotalAmount - viewingReceipt.PaidAmount).toFixed(2)}
                        </span>
                      </div>
                    )}
                </div>
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className="pos-btn neutral" style={{ minHeight: '44px', gap: '8px' }}>
                <RiPrinterLine /> REPRINT
              </button>
              <button
                className="pos-btn warning"
                onClick={() => setViewingReceipt(null)}
                style={{ minHeight: '44px' }}
              >
                CLOSE
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
