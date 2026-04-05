// src/renderer/src/pages/Reports/CreditAccounts.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import styles from './CreditAccounts.module.css'

export default function CreditAccounts() {
  const [invoices, setInvoices] = useState<any[]>([])
  const [loading, setLoading] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')

  // Invoice-based Modal State
  const [selectedInvoice, setSelectedInvoice] = useState<any | null>(null)
  const [paymentAmount, setPaymentAmount] = useState('')
  const [isProcessing, setIsProcessing] = useState(false)

  const loadInvoices = async () => {
    setLoading(true)
    try {
      // @ts-ignore
      const data = await window.api.getPendingCreditAccounts()
      setInvoices(data || [])
    } catch (err) {
      console.error('Failed to load credit invoices', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadInvoices()
  }, [])

  const handleOpenSettle = (invoice: any) => {
    setSelectedInvoice(invoice)
    setPaymentAmount(invoice.TotalPending.toFixed(2))
  }

  const handleProcessPayment = async (e: React.FormEvent) => {
    e.preventDefault()

    const amount = parseFloat(paymentAmount)
    if (isNaN(amount) || amount <= 0) {
      Swal.fire('Invalid Amount', 'Enter a valid payment amount greater than 0.', 'warning')
      return
    }

    const safeAmount = parseFloat(amount.toFixed(2))
    const safePending = parseFloat(selectedInvoice.TotalPending.toFixed(2))

    if (safeAmount > safePending) {
      Swal.fire(
        'Invalid Amount',
        `Payment (Rs ${safeAmount.toFixed(2)}) cannot exceed the total pending debt of Rs ${safePending.toFixed(2)}`,
        'error'
      )
      return
    }

    const confirmResult = await Swal.fire({
      title: 'Process Payment?',
      text: `Process payment of Rs ${safeAmount.toFixed(2)} for Invoice ${selectedInvoice.ReceiptId}?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#16a34a',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, process it!'
    })

    if (confirmResult.isConfirmed) {
      setIsProcessing(true)
      try {
        // @ts-ignore
        await window.api.processCreditPayment(selectedInvoice.ReceiptId, safeAmount)
        Swal.fire('Success!', '✅ Payment successfully applied to invoice!', 'success')
        setSelectedInvoice(null)
        loadInvoices()
      } catch (err: any) {
        Swal.fire('Error', 'Error processing payment: ' + err.message, 'error')
      } finally {
        setIsProcessing(false)
      }
    }
  }

  const displayedInvoices = useMemo(() => {
    if (!searchQuery) return invoices
    const q = searchQuery.toLowerCase()
    return invoices.filter(
      (i) =>
        (i.CustomerName && i.CustomerName.toLowerCase().includes(q)) ||
        i.ReceiptId.toLowerCase().includes(q)
    )
  }, [invoices, searchQuery])

  const totalSystemDebt = useMemo(() => {
    return invoices.reduce((sum, acc) => sum + acc.TotalPending, 0)
  }, [invoices])

  return (
    <div className={styles.container}>
      {/* --- TOP PANEL --- */}
      <div className={styles.topPanel}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
          <h2 className={styles.panelTitle}>DEBTORS LEDGER</h2>
          <input
            type="text"
            className="pos-input"
            style={{ width: '350px' }}
            placeholder="Search Customer or Invoice ID..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
        <div className={styles.summaryBox}>
          <span className={styles.summaryLabel}>Total Market Debt</span>
          <span className={styles.summaryValueDanger}>Rs {totalSystemDebt.toFixed(2)}</span>
        </div>
      </div>

      {/* --- MAIN TABLE --- */}
      <div className={styles.mainPanel}>
        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>DATE</th>
                <th>RECEIPT ID</th>
                <th>CUSTOMER NAME</th>
                <th>TOTAL VALUE</th>
                <th>PAID SO FAR</th>
                <th>CURRENT DEBT</th>
                <th style={{ textAlign: 'right' }}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}
                  >
                    Loading invoices...
                  </td>
                </tr>
              ) : displayedInvoices.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}
                  >
                    {searchQuery
                      ? 'No matching invoices found.'
                      : 'No outstanding credit invoices found.'}
                  </td>
                </tr>
              ) : (
                displayedInvoices.map((inv, idx) => (
                  <tr key={idx}>
                    <td style={{ color: 'var(--text-muted)', fontWeight: 600 }}>
                      {new Date(inv.TransactionDate).toLocaleDateString()}
                    </td>
                    <td
                      style={{
                        fontWeight: 800,
                        fontFamily: 'monospace',
                        color: 'var(--brand-primary)'
                      }}
                    >
                      {inv.ReceiptId}
                    </td>
                    <td style={{ fontWeight: 800 }}>{inv.CustomerName || 'Unknown'}</td>
                    <td style={{ color: 'var(--text-muted)' }}>Rs {inv.TotalCredit.toFixed(2)}</td>
                    <td style={{ color: 'var(--action-success)' }}>
                      Rs {inv.TotalPaid.toFixed(2)}
                    </td>
                    <td
                      style={{ fontWeight: 900, color: 'var(--action-danger)', fontSize: '16px' }}
                    >
                      Rs {inv.TotalPending.toFixed(2)}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        className="pos-btn success"
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                        onClick={() => handleOpenSettle(inv)}
                      >
                        PAY INVOICE
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* --- SETTLE SINGLE INVOICE MODAL --- */}
      {selectedInvoice && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '24px' }}>Pay Invoice</h2>
                <div
                  style={{
                    fontSize: '13px',
                    color: 'var(--text-muted)',
                    fontWeight: 600,
                    marginTop: '4px'
                  }}
                >
                  Customer:{' '}
                  <span style={{ color: 'var(--brand-primary)' }}>
                    {selectedInvoice.CustomerName}
                  </span>{' '}
                  | ID: {selectedInvoice.ReceiptId}
                </div>
              </div>
              <button
                className="pos-btn neutral"
                style={{ minHeight: '40px', padding: '5px 15px' }}
                onClick={() => setSelectedInvoice(null)}
              >
                ✖
              </button>
            </div>

            <div className={styles.modalBody}>
              <div
                style={{
                  background: '#fef2f2',
                  border: '2px solid #fecaca',
                  padding: '20px',
                  borderRadius: '8px',
                  textAlign: 'center',
                  marginBottom: '20px'
                }}
              >
                <div
                  style={{
                    fontSize: '13px',
                    fontWeight: 800,
                    color: '#dc2626',
                    textTransform: 'uppercase'
                  }}
                >
                  Remaining Debt Owed
                </div>
                <div style={{ fontSize: '32px', fontWeight: 900, color: '#dc2626' }}>
                  Rs {selectedInvoice.TotalPending.toFixed(2)}
                </div>
              </div>

              <form
                onSubmit={handleProcessPayment}
                style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}
              >
                <div>
                  <label
                    style={{
                      fontSize: '14px',
                      fontWeight: 800,
                      color: 'var(--text-muted)',
                      textTransform: 'uppercase',
                      marginBottom: '8px',
                      display: 'block'
                    }}
                  >
                    Cash Received (Rs)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    className="pos-input"
                    style={{
                      fontSize: '24px',
                      height: '60px',
                      fontWeight: 900,
                      textAlign: 'center'
                    }}
                    value={paymentAmount}
                    onChange={(e) => setPaymentAmount(e.target.value)}
                    placeholder="0.00"
                    required
                    autoFocus
                  />
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: '1fr 1fr',
                      gap: '10px',
                      marginTop: '15px'
                    }}
                  >
                    <button
                      type="button"
                      className="pos-btn neutral"
                      onClick={() =>
                        setPaymentAmount((selectedInvoice.TotalPending / 2).toFixed(2))
                      }
                    >
                      50% HALF PAY
                    </button>
                    <button
                      type="button"
                      className="pos-btn warning"
                      onClick={() => setPaymentAmount(selectedInvoice.TotalPending.toFixed(2))}
                    >
                      100% FULL PAY
                    </button>
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '15px', marginTop: '10px' }}>
                  <button
                    type="button"
                    className="pos-btn neutral"
                    style={{ flex: 1 }}
                    onClick={() => setSelectedInvoice(null)}
                    disabled={isProcessing}
                  >
                    CANCEL
                  </button>
                  <button
                    type="submit"
                    className="pos-btn success"
                    style={{ flex: 2 }}
                    disabled={isProcessing}
                  >
                    {isProcessing ? 'PROCESSING...' : 'APPLY PAYMENT'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
