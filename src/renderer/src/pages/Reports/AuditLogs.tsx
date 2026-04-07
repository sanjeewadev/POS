// src/renderer/src/pages/Reports/AuditLogs.tsx
import React, { useState, useEffect, useMemo } from 'react'
import {
  RiSearchLine,
  RiCalendarCheckLine,
  RiRefreshLine,
  RiArrowGoBackLine,
  RiCloseCircleLine,
  RiEqualizerLine
} from 'react-icons/ri'
import styles from './AuditLogs.module.css'

export default function AuditLogs() {
  const [logs, setLogs] = useState<any[]>([])
  const [loading, setLoading] = useState(false)

  // Filters
  const [startDate, setStartDate] = useState(() => {
    const d = new Date()
    d.setDate(1)
    return d.toISOString().split('T')[0]
  })
  const [endDate, setEndDate] = useState(() => new Date().toISOString().split('T')[0])
  const [searchQuery, setSearchQuery] = useState('')
  const [filterType, setFilterType] = useState('ALL') // ALL, VOIDS, RETURNS, ADJUSTMENTS

  const loadLogs = async (e?: React.FormEvent) => {
    if (e) e.preventDefault()
    setLoading(true)
    try {
      // @ts-ignore
      const data = await window.api.getAuditLogs(startDate, endDate)
      setLogs(data || [])
    } catch (err) {
      console.error('Failed to load audit logs', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadLogs()
  }, [startDate, endDate])

  const displayedLogs = useMemo(() => {
    let filtered = logs
    if (filterType !== 'ALL') {
      filtered = filtered.filter((log) => {
        if (filterType === 'VOIDS') return log.IsVoided === 1
        if (filterType === 'RETURNS') return log.Type === 4
        if (filterType === 'ADJUSTMENTS') return log.Type === 3
        return true
      })
    }
    if (searchQuery) {
      const q = searchQuery.toLowerCase()
      filtered = filtered.filter(
        (log) =>
          (log.ReceiptId && log.ReceiptId.toLowerCase().includes(q)) ||
          (log.ProductName && log.ProductName.toLowerCase().includes(q)) ||
          (log.Note && log.Note.toLowerCase().includes(q))
      )
    }
    return filtered
  }, [logs, filterType, searchQuery])

  const getEventDetails = (log: any) => {
    const qty = parseFloat(log.Quantity) || 0
    const price = parseFloat(log.UnitPrice) || 0
    const cost = parseFloat(log.UnitCost) || 0

    if (log.IsVoided === 1) {
      return {
        badge: (
          <span className={`${styles.badge} ${styles.badgeVoid}`}>
            <RiCloseCircleLine /> VOIDED SALE
          </span>
        ),
        impactLabel: 'Reversed Revenue',
        financial: qty * price,
        stockDir: '+',
        stockClass: styles.stockPositive
      }
    }
    if (log.Type === 4) {
      return {
        badge: (
          <span className={`${styles.badge} ${styles.badgeReturn}`}>
            <RiArrowGoBackLine /> CUSTOMER RETURN
          </span>
        ),
        impactLabel: 'Refund Given',
        financial: qty * price,
        stockDir: '+',
        stockClass: styles.stockPositive
      }
    }
    if (log.Type === 3) {
      return {
        badge: (
          <span className={`${styles.badge} ${styles.badgeAdjust}`}>
            <RiEqualizerLine /> ADJUSTMENT
          </span>
        ),
        impactLabel: 'Loss / Value',
        financial: qty * cost,
        stockDir: '-',
        stockClass: styles.stockNegative
      }
    }
    return {
      badge: <span>UNKNOWN</span>,
      impactLabel: '-',
      financial: 0,
      stockDir: '',
      stockClass: ''
    }
  }

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <div className={styles.titleArea}>
            {/* 🚀 Applied Global Title */}
            <h2 className="pos-page-title">Security & Audit Logs</h2>
            <p className={styles.pageSubtitle}>Tracking Voids, Returns, and Manual Adjustments</p>
          </div>

          <form onSubmit={loadLogs} className={styles.filterForm}>
            <div className={styles.inputStack}>
              <label>
                <RiCalendarCheckLine /> FROM
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
                <RiCalendarCheckLine /> TO
              </label>
              <input
                type="date"
                className="pos-input"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                required
              />
            </div>
            <button
              type="submit"
              className={`pos-btn neutral ${styles.refreshBtn}`}
              disabled={loading}
            >
              <RiRefreshLine className={loading ? styles.spin : ''} /> REFRESH
            </button>
          </form>
        </div>

        <div className={styles.panelBody}>
          <div className={styles.controlBar}>
            <div className={styles.searchWrapper}>
              <RiSearchLine className={styles.searchIcon} />
              <input
                type="text"
                className={`pos-input ${styles.searchInput}`}
                placeholder="Search Product, Receipt ID, or Note..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className={styles.tabGroup}>
              <button
                className={`${styles.filterBtn} ${filterType === 'ALL' ? styles.activeAll : ''}`}
                onClick={() => setFilterType('ALL')}
              >
                ALL EVENTS
              </button>
              <button
                className={`${styles.filterBtn} ${filterType === 'VOIDS' ? styles.activeVoid : ''}`}
                onClick={() => setFilterType('VOIDS')}
              >
                VOIDS
              </button>
              <button
                className={`${styles.filterBtn} ${filterType === 'RETURNS' ? styles.activeReturn : ''}`}
                onClick={() => setFilterType('RETURNS')}
              >
                RETURNS
              </button>
              <button
                className={`${styles.filterBtn} ${filterType === 'ADJUSTMENTS' ? styles.activeAdjust : ''}`}
                onClick={() => setFilterType('ADJUSTMENTS')}
              >
                ADJUSTMENTS
              </button>
            </div>
          </div>

          <div className={styles.tableWrapper}>
            <table className={styles.classicTable}>
              <thead>
                <tr>
                  <th className={styles.colDate}>DATE & TIME</th>
                  <th className={styles.colEvent}>EVENT TYPE</th>
                  <th className={styles.colProduct}>PRODUCT</th>
                  <th className={styles.colRef}>REFERENCE</th>
                  <th className={styles.colStock}>STOCK IMPACT</th>
                  <th className={styles.colImpact}>FINANCIAL IMPACT</th>
                </tr>
              </thead>
              <tbody>
                {displayedLogs.length === 0 ? (
                  <tr>
                    <td colSpan={6} className={styles.emptyState}>
                      No security events found for the selected filters.
                    </td>
                  </tr>
                ) : (
                  displayedLogs.map((log, idx) => {
                    const details = getEventDetails(log)
                    return (
                      <tr key={log.Id || idx}>
                        <td className={styles.cellDate}>{new Date(log.Date).toLocaleString()}</td>
                        <td>{details.badge}</td>
                        <td className={styles.cellProduct}>
                          <div
                            className={styles.truncatedText}
                            title={log.ProductName || 'System Event'}
                          >
                            {log.ProductName || 'System Event'}
                          </div>
                        </td>
                        <td className={styles.cellRef}>
                          {log.ReceiptId && <div className={styles.receiptId}>{log.ReceiptId}</div>}
                          <div className={styles.noteText} title={log.Note || '-'}>
                            {log.Note || '-'}
                          </div>
                        </td>
                        <td className={`${styles.cellStock} ${details.stockClass}`}>
                          {details.stockDir}
                          {parseFloat(log.Quantity) || 0}{' '}
                          <span className={styles.unitText}>{log.Unit}</span>
                        </td>
                        <td className={styles.cellImpact}>
                          <div className={styles.impactLabel}>{details.impactLabel}</div>
                          <div className={styles.impactValue}>
                            Rs {details.financial.toFixed(2)}
                          </div>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  )
}
