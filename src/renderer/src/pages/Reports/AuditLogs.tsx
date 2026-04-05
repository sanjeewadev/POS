// src/renderer/src/pages/Reports/AuditLogs.tsx
import React, { useState, useEffect, useMemo } from 'react'
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
        badge: <span className={`${styles.badge} ${styles.badgeVoid}`}>VOIDED SALE</span>,
        impactLabel: 'Reversed Revenue',
        financial: qty * price,
        stockDir: '+',
        stockColor: 'var(--action-success)'
      }
    }
    if (log.Type === 4) {
      return {
        badge: <span className={`${styles.badge} ${styles.badgeReturn}`}>CUSTOMER RETURN</span>,
        impactLabel: 'Refund Given',
        financial: qty * price,
        stockDir: '+',
        stockColor: 'var(--action-success)'
      }
    }
    if (log.Type === 3) {
      return {
        badge: <span className={`${styles.badge} ${styles.badgeAdjust}`}>STOCK ADJUSTMENT</span>,
        impactLabel: 'Loss / Value',
        financial: qty * cost,
        stockDir: '-',
        stockColor: 'var(--action-danger)'
      }
    }
    return {
      badge: <span>UNKNOWN</span>,
      impactLabel: '-',
      financial: 0,
      stockDir: '',
      stockColor: 'var(--text-main)'
    }
  }

  return (
    <div className={styles.container}>
      {/* --- TOP PANEL: FILTERS --- */}
      <div className={styles.topPanel}>
        <div>
          <h2 className={styles.panelTitle}>SECURITY & AUDIT LOGS</h2>
          <p
            style={{
              margin: '5px 0 0 0',
              color: 'var(--text-muted)',
              fontSize: '13px',
              fontWeight: 600
            }}
          >
            Tracking Voids, Returns, and Manual Adjustments
          </p>
        </div>

        <form onSubmit={loadLogs} style={{ display: 'flex', gap: '15px', alignItems: 'flex-end' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '11px', fontWeight: 800, color: 'var(--text-muted)' }}>
              FROM
            </label>
            <input
              type="date"
              className="pos-input"
              style={{ padding: '8px' }}
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              required
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '11px', fontWeight: 800, color: 'var(--text-muted)' }}>
              TO
            </label>
            <input
              type="date"
              className="pos-input"
              style={{ padding: '8px' }}
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              required
            />
          </div>
          <button
            type="submit"
            className="pos-btn neutral"
            style={{ minHeight: '42px', padding: '10px 20px', fontSize: '12px' }}
            disabled={loading}
          >
            {loading ? 'LOADING...' : 'REFRESH NOW'}
          </button>
        </form>
      </div>

      {/* --- MIDDLE PANEL: SEARCH & TABS --- */}
      <div className={styles.controlBar}>
        <input
          type="text"
          className="pos-input"
          style={{ flex: 1 }}
          placeholder="Search Product, Receipt ID, or Note..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
        <div className={styles.tabGroup}>
          <button
            className={`pos-btn ${filterType === 'ALL' ? 'success' : 'neutral'}`}
            style={{ minHeight: '40px', padding: '10px 20px', fontSize: '12px' }}
            onClick={() => setFilterType('ALL')}
          >
            ALL EVENTS
          </button>
          <button
            className={`pos-btn ${filterType === 'VOIDS' ? 'warning' : 'neutral'}`}
            style={{ minHeight: '40px', padding: '10px 20px', fontSize: '12px' }}
            onClick={() => setFilterType('VOIDS')}
          >
            VOIDS
          </button>
          <button
            className={`pos-btn ${filterType === 'RETURNS' ? 'warning' : 'neutral'}`}
            style={{ minHeight: '40px', padding: '10px 20px', fontSize: '12px' }}
            onClick={() => setFilterType('RETURNS')}
          >
            RETURNS
          </button>
          <button
            className={`pos-btn ${filterType === 'ADJUSTMENTS' ? 'danger' : 'neutral'}`}
            style={{ minHeight: '40px', padding: '10px 20px', fontSize: '12px' }}
            onClick={() => setFilterType('ADJUSTMENTS')}
          >
            ADJUSTMENTS
          </button>
        </div>
      </div>

      {/* --- MAIN TABLE --- */}
      <div className={styles.mainPanel}>
        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>DATE & TIME</th>
                <th>EVENT TYPE</th>
                <th>PRODUCT</th>
                <th>REFERENCE / NOTE</th>
                <th style={{ textAlign: 'center' }}>QTY IMPACT</th>
                <th style={{ textAlign: 'right' }}>FINANCIAL IMPACT</th>
              </tr>
            </thead>
            <tbody>
              {displayedLogs.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}
                  >
                    No security events found for the selected filters.
                  </td>
                </tr>
              ) : (
                displayedLogs.map((log, idx) => {
                  const details = getEventDetails(log)
                  return (
                    <tr key={log.Id || idx}>
                      <td style={{ color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>
                        {new Date(log.Date).toLocaleString()}
                      </td>
                      <td>{details.badge}</td>
                      <td style={{ fontWeight: 800 }}>{log.ProductName || 'System Event'}</td>
                      <td style={{ fontSize: '12px' }}>
                        {log.ReceiptId && (
                          <strong
                            style={{
                              color: 'var(--brand-primary)',
                              fontFamily: 'monospace',
                              fontSize: '14px'
                            }}
                          >
                            {log.ReceiptId} <br />
                          </strong>
                        )}
                        <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>
                          {log.Note || '-'}
                        </span>
                      </td>
                      <td
                        style={{
                          textAlign: 'center',
                          fontWeight: 900,
                          fontSize: '16px',
                          color: details.stockColor
                        }}
                      >
                        {details.stockDir}
                        {parseFloat(log.Quantity) || 0}{' '}
                        <span
                          style={{
                            fontSize: '11px',
                            fontWeight: 'normal',
                            color: 'var(--text-muted)'
                          }}
                        >
                          {log.Unit || ''}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div
                          style={{
                            fontSize: '11px',
                            color: 'var(--text-muted)',
                            textTransform: 'uppercase',
                            fontWeight: 800
                          }}
                        >
                          {details.impactLabel}
                        </div>
                        <div
                          style={{ fontWeight: 900, color: 'var(--text-dark)', fontSize: '16px' }}
                        >
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
  )
}
