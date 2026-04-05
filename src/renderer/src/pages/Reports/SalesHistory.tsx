// src/renderer/src/pages/Reports/SalesHistory.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import styles from './SalesHistory.module.css'

export default function SalesHistory() {
  const [sales, setSales] = useState<any[]>([])
  const [loading, setLoading] = useState(false)

  // Filters: Default to the current month
  const [searchQuery, setSearchQuery] = useState('')
  const [startDate, setStartDate] = useState(() => {
    const d = new Date()
    d.setDate(1) // Start of current month
    return d.toISOString().split('T')[0]
  })
  const [endDate, setEndDate] = useState(() => new Date().toISOString().split('T')[0])

  // Modal State
  const [viewingReceipt, setViewingReceipt] = useState<any | null>(null)
  const [receiptItems, setReceiptItems] = useState<any[]>([])

  const loadSalesHistory = async (e?: React.FormEvent) => {
    if (e) e.preventDefault()
    setLoading(true)
    try {
      // @ts-ignore
      const data = await window.api.getSalesHistory(startDate, endDate, searchQuery.trim())
      setSales(data || [])
    } catch (err: any) {
      console.error('Failed to load sales history', err)
      Swal.fire('Error', 'Failed to load sales history: ' + err.message, 'error')
    } finally {
      setLoading(false)
    }
  }

  // Auto-reload when dates are changed
  useEffect(() => {
    loadSalesHistory()
  }, [startDate, endDate])

  const handleViewItems = async (txn: any) => {
    setViewingReceipt(txn)
    try {
      // @ts-ignore
      const items = await window.api.getReceiptItems(txn.ReceiptId)
      setReceiptItems(items || [])
    } catch (err: any) {
      Swal.fire('Error', 'Failed to load receipt details.', 'error')
      setReceiptItems([])
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

  const periodTotals = useMemo(() => {
    let validSales = 0
    let voidedSales = 0
    sales.forEach((s) => {
      if (s.Status === 3) voidedSales += s.TotalAmount
      else validSales += s.TotalAmount
    })
    return { validSales, voidedSales }
  }, [sales])

  const totalSavings = receiptItems.reduce((sum, item) => {
    const original = item.UnitCost || item.UnitPrice
    return sum + Math.max(0, original - item.UnitPrice) * item.Quantity
  }, 0)

  return (
    <div className={styles.container}>
      {/* --- TOP: FILTER & SUMMARY BAR --- */}
      <div className={styles.topPanel}>
        <form onSubmit={loadSalesHistory} className={styles.filterGroup}>
          <div className={styles.dateFilters}>
            <div className={styles.inputStack}>
              <label>FROM DATE</label>
              <input
                type="date"
                className="pos-input"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                required
              />
            </div>
            <div className={styles.inputStack}>
              <label>TO DATE</label>
              <input
                type="date"
                className="pos-input"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                required
              />
            </div>
          </div>

          <div className={styles.searchStack}>
            <label>SEARCH ID / CUSTOMER</label>
            <div style={{ display: 'flex', gap: '10px' }}>
              <input
                type="text"
                className="pos-input"
                placeholder="INV-..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
              <button
                type="submit"
                className="pos-btn success"
                style={{ minWidth: '150px' }}
                disabled={loading}
              >
                {loading ? '...' : 'LOAD'}
              </button>
            </div>
          </div>
        </form>

        <div className={styles.summaryGroup}>
          <div className={styles.summaryBox}>
            <span className={styles.summaryLabel}>Total Net Revenue</span>
            <span className={styles.summaryValueSuccess}>
              Rs {periodTotals.validSales.toFixed(2)}
            </span>
          </div>
          <div className={styles.summaryBox}>
            <span className={styles.summaryLabel}>Total Voided</span>
            <span className={styles.summaryValueDanger}>
              Rs {periodTotals.voidedSales.toFixed(2)}
            </span>
          </div>
        </div>
      </div>

      {/* --- BOTTOM: MAIN TABLE --- */}
      <div className={styles.mainPanel}>
        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>DATE & TIME</th>
                <th>RECEIPT ID</th>
                <th>CUSTOMER</th>
                <th>PAYMENT</th>
                <th>TOTAL</th>
                <th>STATUS</th>
                <th style={{ textAlign: 'right' }}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {sales.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{ textAlign: 'center', padding: '50px', color: 'var(--text-muted)' }}
                  >
                    No records found for this period.
                  </td>
                </tr>
              ) : (
                sales.map((s) => (
                  <tr key={s.ReceiptId} className={s.Status === 3 ? styles.voidedRow : ''}>
                    <td>
                      <div style={{ fontWeight: 800 }}>
                        {new Date(s.TransactionDate).toLocaleDateString()}
                      </div>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                        {new Date(s.TransactionDate).toLocaleTimeString()}
                      </div>
                    </td>
                    <td
                      style={{
                        fontWeight: 800,
                        fontFamily: 'monospace',
                        color: 'var(--brand-primary)'
                      }}
                    >
                      {s.ReceiptId}
                    </td>
                    <td style={{ fontWeight: 600 }}>{s.CustomerName || 'Walk-in'}</td>
                    <td>
                      <span className={s.IsCredit ? styles.creditBadge : styles.cashBadge}>
                        {s.IsCredit ? 'CREDIT' : 'CASH'}
                      </span>
                    </td>
                    <td style={{ fontWeight: 900 }}>Rs {s.TotalAmount.toFixed(2)}</td>
                    <td>{renderStatusBadge(s.Status)}</td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        className="pos-btn neutral"
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                        onClick={() => handleViewItems(s)}
                      >
                        VIEW DETAILS
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* --- RECEIPT DETAILS MODAL --- */}
      {viewingReceipt && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '24px' }}>
                  Invoice History: {viewingReceipt.ReceiptId}
                </h2>
                <div
                  style={{
                    fontSize: '13px',
                    color: 'var(--text-muted)',
                    fontWeight: 600,
                    marginTop: '4px'
                  }}
                >
                  Processed: {new Date(viewingReceipt.TransactionDate).toLocaleString()}
                </div>
              </div>
              <button
                className="pos-btn neutral"
                onClick={() => setViewingReceipt(null)}
                style={{ minHeight: '40px', padding: '5px 15px' }}
              >
                ✖
              </button>
            </div>

            <div className={styles.modalBody}>
              <table className={styles.classicTable}>
                <thead>
                  <tr>
                    <th>SKU</th>
                    <th>ITEM NAME</th>
                    <th>QTY</th>
                    <th>RETAIL</th>
                    <th>DISC</th>
                    <th>SOLD AT</th>
                    <th style={{ textAlign: 'right' }}>TOTAL</th>
                  </tr>
                </thead>
                <tbody>
                  {receiptItems.map((item, idx) => {
                    const original = item.UnitCost || item.UnitPrice
                    const discount = Math.max(0, original - item.UnitPrice)
                    return (
                      <tr key={idx}>
                        <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>
                          {item.Barcode || '-'}
                        </td>
                        <td style={{ fontWeight: 800 }}>{item.ProductName}</td>
                        <td style={{ fontWeight: 900 }}>
                          {item.Quantity} {item.Unit}
                        </td>
                        <td
                          style={{
                            color: 'var(--text-muted)',
                            textDecoration: discount > 0 ? 'line-through' : 'none'
                          }}
                        >
                          Rs {original.toFixed(2)}
                        </td>
                        <td style={{ color: 'var(--action-warning)', fontWeight: 800 }}>
                          {discount > 0 ? `-Rs ${discount.toFixed(2)}` : '-'}
                        </td>
                        <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                          Rs {item.UnitPrice.toFixed(2)}
                        </td>
                        <td style={{ fontWeight: 900, textAlign: 'right' }}>
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
                    <div className={styles.savingsTag}>
                      ⚡ Total Discounts Applied: Rs {totalSavings.toFixed(2)}
                    </div>
                  )}
                  <div style={{ marginTop: '15px' }}>
                    <div className={styles.summaryLine}>
                      <span>Processed By:</span>{' '}
                      <span style={{ color: 'var(--text-dark)' }}>System Admin</span>
                    </div>
                    <div className={styles.summaryLine}>
                      <span>Payment:</span>{' '}
                      <span style={{ color: 'var(--brand-primary)' }}>
                        {viewingReceipt.IsCredit ? 'Credit Account' : 'Cash'}
                      </span>
                    </div>
                  </div>
                </div>

                <div className={styles.summaryRight}>
                  {!viewingReceipt.IsCredit && viewingReceipt.CashReceived > 0 && (
                    <div className={styles.summaryLine} style={{ color: 'var(--text-muted)' }}>
                      <span>Cash Tendered:</span>
                      <span>Rs {viewingReceipt.CashReceived.toFixed(2)}</span>
                    </div>
                  )}
                  {!viewingReceipt.IsCredit && viewingReceipt.ChangeGiven > 0 && (
                    <div className={styles.summaryLine} style={{ color: 'var(--text-muted)' }}>
                      <span>Change Given:</span>
                      <span>Rs {viewingReceipt.ChangeGiven.toFixed(2)}</span>
                    </div>
                  )}
                  <div className={styles.summaryLine}>
                    <span>Bill Total:</span>
                    <span>Rs {viewingReceipt.TotalAmount.toFixed(2)}</span>
                  </div>
                  <div className={`${styles.summaryLine} ${styles.paidLine}`}>
                    <span>Paid:</span>
                    <span>Rs {viewingReceipt.PaidAmount.toFixed(2)}</span>
                  </div>
                  {viewingReceipt.TotalAmount - viewingReceipt.PaidAmount > 0 && (
                    <div className={`${styles.summaryLine} ${styles.balanceLine}`}>
                      <span>Remaining Debt:</span>
                      <span>
                        Rs {(viewingReceipt.TotalAmount - viewingReceipt.PaidAmount).toFixed(2)}
                      </span>
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className="pos-btn neutral">🖨️ PRINT COPY</button>
              <button className="pos-btn warning" onClick={() => setViewingReceipt(null)}>
                CLOSE
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
