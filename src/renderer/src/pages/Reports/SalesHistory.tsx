// src/renderer/src/pages/Reports/SalesHistory.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import {
  RiCalendarEventLine,
  RiSearchLine,
  RiPrinterLine,
  RiCloseLine,
  RiInformationLine,
  RiMoneyDollarCircleLine,
  RiErrorWarningLine
} from 'react-icons/ri'
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
      <div className={styles.mainPanel}>
        {/* 🚀 UPGRADED 50/50 HEADER */}
        <div className={styles.panelHeader}>
          <div className={styles.headerLeft}>
            <h2 className="pos-page-title">Sales History & Archives</h2>
          </div>
          <div className={styles.headerRight}>
            <form onSubmit={loadSalesHistory} className={styles.searchForm}>
              <div className={styles.searchInputWrapper}>
                <RiSearchLine className={styles.searchIcon} />
                <input
                  type="text"
                  className={`pos-input ${styles.searchInput}`}
                  placeholder="Search Invoice ID or Customer Name..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
              <button
                type="submit"
                className={`pos-btn success ${styles.loadBtn}`}
                disabled={loading}
              >
                {loading ? '...' : 'LOAD'}
              </button>
            </form>
          </div>
        </div>

        <div className={styles.panelBody}>
          {/* TOP: FILTER & SUMMARY BAR */}
          <div className={styles.topPanel}>
            <div className={styles.dateFilters}>
              <div className={styles.inputStack}>
                <label>
                  <RiCalendarEventLine /> FROM DATE
                </label>
                <input
                  type="date"
                  className="pos-input"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  required
                />
              </div>
              <div className={styles.inputStack}>
                <label>
                  <RiCalendarEventLine /> TO DATE
                </label>
                <input
                  type="date"
                  className="pos-input"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                  required
                />
              </div>
            </div>

            <div className={styles.summaryGroup}>
              <div className={styles.summaryBox}>
                <RiMoneyDollarCircleLine size={24} color="var(--action-success)" />
                <div>
                  <span className={styles.summaryLabel}>Net Revenue</span>
                  <span className={styles.summaryValueSuccess}>
                    Rs {periodTotals.validSales.toFixed(2)}
                  </span>
                </div>
              </div>
              <div className={styles.summaryBox}>
                <RiErrorWarningLine size={24} color="var(--action-danger)" />
                <div>
                  <span className={styles.summaryLabel}>Total Voided</span>
                  <span className={styles.summaryValueDanger}>
                    Rs {periodTotals.voidedSales.toFixed(2)}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* MAIN TABLE */}
          <div className={styles.tableWrapper}>
            <table className={styles.classicTable}>
              <thead>
                <tr>
                  <th className={styles.colDate}>DATE & TIME</th>
                  <th className={styles.colId}>RECEIPT ID</th>
                  <th className={styles.colCustomer}>CUSTOMER</th>
                  <th className={styles.colPayment}>PAYMENT</th>
                  <th className={styles.colTotal}>TOTAL</th>
                  <th className={styles.colStatus}>STATUS</th>
                  <th className={styles.textRight}>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {sales.length === 0 ? (
                  <tr>
                    <td colSpan={7} className={styles.emptyState}>
                      No records found for this period.
                    </td>
                  </tr>
                ) : (
                  sales.map((s) => (
                    <tr key={s.ReceiptId} className={s.Status === 3 ? styles.voidedRow : ''}>
                      <td>
                        <div className={styles.cellDarkBoldMd}>
                          {new Date(s.TransactionDate).toLocaleDateString()}
                        </div>
                        <div className={styles.timeMeta}>
                          {new Date(s.TransactionDate).toLocaleTimeString()}
                        </div>
                      </td>
                      <td className={styles.receiptIdCell}>{s.ReceiptId}</td>
                      <td className={styles.cellDarkBoldSm}>{s.CustomerName || 'Walk-in'}</td>
                      <td>
                        <span className={s.IsCredit ? styles.creditBadge : styles.cashBadge}>
                          {s.IsCredit ? 'CREDIT' : 'CASH'}
                        </span>
                      </td>
                      <td className={styles.cellDarkBoldLg}>Rs {s.TotalAmount.toFixed(2)}</td>
                      <td>{renderStatusBadge(s.Status)}</td>
                      <td className={styles.textRight}>
                        {/* 🚀 Changed to pos-btn-sm */}
                        <button className="pos-btn-sm neutral" onClick={() => handleViewItems(s)}>
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
      </div>

      {/* --- RECEIPT DETAILS MODAL --- */}
      {viewingReceipt && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 className="pos-page-title">Invoice: {viewingReceipt.ReceiptId}</h2>
                <div className={styles.modalSubHeader}>
                  <RiInformationLine /> Processed:{' '}
                  {new Date(viewingReceipt.TransactionDate).toLocaleString()}
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
                    <th className={styles.colItemName}>ITEM NAME</th>
                    <th className={styles.colQty}>QTY</th>
                    <th className={styles.colRetail}>RETAIL</th>
                    <th className={styles.colDisc}>DISC</th>
                    <th className={styles.colSoldAt}>SOLD AT</th>
                    <th className={styles.textRight}>TOTAL</th>
                  </tr>
                </thead>
                <tbody>
                  {receiptItems.map((item, idx) => {
                    const original = item.UnitCost || item.UnitPrice
                    const discount = Math.max(0, original - item.UnitPrice)
                    return (
                      <tr key={idx}>
                        <td className={styles.cellDarkBoldMd}>{item.ProductName}</td>
                        <td className={styles.cellDarkBoldLg}>
                          {item.Quantity} <span className={styles.unitSpan}>{item.Unit}</span>
                        </td>
                        <td className={discount > 0 ? styles.strikethrough : ''}>
                          Rs {original.toFixed(2)}
                        </td>
                        <td className={styles.cellWarning}>
                          {discount > 0 ? `-Rs ${discount.toFixed(2)}` : '-'}
                        </td>
                        <td className={styles.cellSuccess}>Rs {item.UnitPrice.toFixed(2)}</td>
                        <td className={`${styles.cellDarkBoldLg} ${styles.textRight}`}>
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
                      ⚡ Discounts: Rs {totalSavings.toFixed(2)}
                    </div>
                  )}
                  <div className={styles.paymentMeta}>
                    <strong>Payment Method:</strong>{' '}
                    {viewingReceipt.IsCredit ? 'Credit Account' : 'Cash'}
                  </div>
                </div>
                <div className={styles.summaryRight}>
                  <div className={styles.summaryLine}>
                    <span>Bill Total:</span> <span>Rs {viewingReceipt.TotalAmount.toFixed(2)}</span>
                  </div>
                  <div className={`${styles.summaryLine} ${styles.paidLine}`}>
                    <span>Paid:</span> <span>Rs {viewingReceipt.PaidAmount.toFixed(2)}</span>
                  </div>
                  {viewingReceipt.TotalAmount - viewingReceipt.PaidAmount > 0 && (
                    <div className={`${styles.summaryLine} ${styles.balanceLine}`}>
                      <span>Remaining:</span>{' '}
                      <span>
                        Rs {(viewingReceipt.TotalAmount - viewingReceipt.PaidAmount).toFixed(2)}
                      </span>
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className={`pos-btn neutral ${styles.footerBtn}`}>
                <RiPrinterLine /> PRINT COPY
              </button>
              <button
                className={`pos-btn warning ${styles.footerBtn}`}
                onClick={() => setViewingReceipt(null)}
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
