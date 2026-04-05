// src/renderer/src/pages/Returns/ReturnsCenter.tsx
import { useState, useMemo, useRef, useEffect } from 'react'
import Swal from 'sweetalert2'
import styles from './ReturnsCenter.module.css'

interface ReturnItem {
  ProductId: number
  ProductName: string
  Unit: string
  StockBatchId: number
  UnitPrice: number
  UnitCost: number
  OriginalQty: number
  ReturnedQty: number
  QtyToReturn: string
  QuantityType: string // 🚀 NEW: Need this to validate decimals
}

export default function ReturnsCenter() {
  const [searchQuery, setSearchQuery] = useState('')
  const [loading, setLoading] = useState(false)

  const [bill, setBill] = useState<any | null>(null)
  const [items, setItems] = useState<ReturnItem[]>([])
  const [returnReason, setReturnReason] = useState('Customer Changed Mind')

  const searchInputRef = useRef<HTMLInputElement>(null)

  // Auto-focus search on load
  useEffect(() => {
    searchInputRef.current?.focus()
  }, [])

  // --- 1. SEARCH FOR THE BILL ---
  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!searchQuery) return

    setLoading(true)
    setBill(null)
    setItems([])

    try {
      // @ts-ignore
      const result = await window.api.getBillForReturn(searchQuery.trim())

      if (!result || !result.transaction) {
        Swal.fire('Not Found', 'Invoice not found. Please check the receipt number.', 'error')
        setLoading(false)
        return
      }

      setBill(result.transaction)

      const mappedItems = result.items.map((item: any) => ({
        ...item,
        QtyToReturn: ''
      }))
      setItems(mappedItems)
    } catch (err: any) {
      console.error(err)
      Swal.fire('Error', 'Error searching for bill: ' + err.message, 'error')
    } finally {
      setLoading(false)
    }
  }

  // --- 2. HANDLE QUANTITY TYPING ---
  const handleQtyChange = (index: number, val: string) => {
    const newItems = [...items]
    const item = newItems[index]
    const maxReturnable = item.OriginalQty - item.ReturnedQty

    if (val === '' || /^\d*\.?\d*$/.test(val)) {
      const numVal = parseFloat(val) || 0

      // 🚀 Decimal Validation
      if (item.QuantityType === 'quantity' && numVal % 1 !== 0) {
        Swal.fire('Invalid Qty', `Cannot return partial amounts of ${item.ProductName}.`, 'error')
        return
      }

      if (numVal <= maxReturnable) {
        item.QtyToReturn = val
        setItems(newItems)
      }
    }
  }

  // --- 3. CALCULATE REFUND TOTAL ---
  const totalRefundAmount = useMemo(() => {
    let total = 0
    items.forEach((item) => {
      const qty = parseFloat(item.QtyToReturn) || 0
      total += qty * item.UnitPrice
    })
    return total
  }, [items])

  // --- 4. PROCESS THE RETURN ---
  const handleProcessReturn = async () => {
    const itemsToReturn = items.filter((item) => (parseFloat(item.QtyToReturn) || 0) > 0)

    if (itemsToReturn.length === 0) {
      return Swal.fire(
        'Missing Information',
        'Please enter a quantity to return for at least one item.',
        'warning'
      )
    }

    const safeReason = returnReason.trim() || 'Manual Return (No Reason Provided)'

    let confirmMessage = `<div style="text-align: left;"><strong>Items to be returned to stock:</strong><ul style="margin-top: 10px;">`
    itemsToReturn.forEach((item) => {
      confirmMessage += `<li>${item.QtyToReturn} ${item.Unit} of <b>${item.ProductName}</b></li>`
    })
    confirmMessage += `</ul><div style="background: #fef2f2; border: 2px solid #fecaca; color: #dc2626; padding: 15px; border-radius: 8px; margin-top: 15px; font-size: 18px; font-weight: 900; text-align: center;">Total Cash to Refund Customer:<br/>Rs ${totalRefundAmount.toFixed(2)}</div></div>`

    const confirmResult = await Swal.fire({
      title: 'Process Return?',
      html: confirmMessage,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, Process Return & Refund'
    })

    if (confirmResult.isConfirmed) {
      try {
        const payload = {
          ReceiptId: bill.ReceiptId,
          RefundAmount: totalRefundAmount,
          Items: itemsToReturn.map((item) => ({
            ProductId: item.ProductId,
            Quantity: parseFloat(item.QtyToReturn),
            UnitCost: item.UnitCost,
            UnitPrice: item.UnitPrice,
            StockBatchId: item.StockBatchId,
            Note: safeReason
          }))
        }

        // @ts-ignore
        await window.api.processReturn(payload)
        Swal.fire('Success', '✅ Return processed successfully!', 'success')

        // Refresh the view
        setSearchQuery(bill.ReceiptId)
        document
          .getElementById('searchForm')
          ?.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }))
      } catch (err: any) {
        Swal.fire('Error', err.message || 'Error processing return.', 'error')
      }
    }
  }

  return (
    <div className={styles.container}>
      {/* THE SEARCH BAR */}
      <div className={styles.searchPanel}>
        <h2 className={styles.panelTitle}>RETURNS CENTER</h2>
        <form id="searchForm" onSubmit={handleSearch} className={styles.searchForm}>
          <input
            ref={searchInputRef}
            type="text"
            className="pos-input"
            style={{ flex: 1, height: '60px', fontSize: '20px' }}
            placeholder="Scan or type Receipt ID (e.g., INV-1234)..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          <button
            type="submit"
            className="pos-btn success"
            style={{ height: '60px', padding: '0 40px' }}
            disabled={loading}
          >
            {loading ? 'SEARCHING...' : 'FIND RECEIPT'}
          </button>
        </form>
      </div>

      {/* THE RESULTS AREA */}
      <div className={styles.mainArea}>
        {!bill ? (
          <div className={styles.emptyState}>
            <h2>Enter a Receipt ID to begin a return.</h2>
            <p>You can process partial returns or full returns here.</p>
          </div>
        ) : (
          <div className={styles.billPanel}>
            {/* BILL HEADER */}
            <div className={styles.billHeader}>
              <div>
                <h2
                  style={{
                    margin: 0,
                    fontSize: '28px',
                    color: 'var(--text-main)',
                    fontWeight: 900
                  }}
                >
                  Receipt: {bill.ReceiptId}
                </h2>
                <div
                  style={{
                    fontSize: '14px',
                    color: 'var(--text-muted)',
                    marginTop: '8px',
                    fontWeight: 600
                  }}
                >
                  Date: {new Date(bill.TransactionDate).toLocaleString()} | Customer:{' '}
                  <span style={{ color: 'var(--brand-primary)', fontWeight: 800 }}>
                    {bill.CustomerName || 'Walk-in'}
                  </span>
                </div>
              </div>
              <div
                style={{
                  textAlign: 'right',
                  background: 'var(--bg-main)',
                  padding: '15px 25px',
                  borderRadius: '8px'
                }}
              >
                <div
                  style={{
                    fontSize: '12px',
                    color: 'var(--text-muted)',
                    fontWeight: 800,
                    textTransform: 'uppercase'
                  }}
                >
                  Original Bill Total
                </div>
                <div style={{ fontSize: '32px', fontWeight: 900, color: 'var(--text-main)' }}>
                  Rs {bill.TotalAmount.toFixed(2)}
                </div>
              </div>
            </div>

            {/* STATUS WARNINGS */}
            {bill.Status === 3 && (
              <div className={styles.dangerBanner}>
                ⚠️ This entire receipt has already been VOIDED. You cannot process returns on a
                voided bill.
              </div>
            )}

            {/* ITEMS TABLE */}
            <div className={styles.tableWrapper}>
              <table className={styles.classicTable}>
                <thead>
                  <tr>
                    <th>ITEM NAME</th>
                    <th>SOLD PRICE</th>
                    <th>BOUGHT</th>
                    <th>ALREADY RETURNED</th>
                    <th>MAX RETURNABLE</th>
                    <th style={{ width: '180px' }}>QTY TO RETURN</th>
                    <th style={{ textAlign: 'right' }}>REFUND (Rs)</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item, idx) => {
                    const maxReturnable = item.OriginalQty - item.ReturnedQty
                    const isFullyReturned = maxReturnable <= 0
                    const currentReturnQty = parseFloat(item.QtyToReturn) || 0
                    const refundLineTotal = currentReturnQty * item.UnitPrice

                    return (
                      <tr key={idx} className={isFullyReturned ? styles.rowDisabled : ''}>
                        <td style={{ fontWeight: 800, fontSize: '16px' }}>{item.ProductName}</td>
                        <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                          Rs {item.UnitPrice.toFixed(2)}
                        </td>
                        <td style={{ fontWeight: 800 }}>
                          {item.OriginalQty}{' '}
                          <span
                            style={{
                              fontSize: '11px',
                              color: 'var(--text-muted)',
                              fontWeight: 'normal'
                            }}
                          >
                            {item.Unit}
                          </span>
                        </td>
                        <td style={{ color: 'var(--action-danger)', fontWeight: 800 }}>
                          {item.ReturnedQty}{' '}
                          <span
                            style={{
                              fontSize: '11px',
                              color: 'var(--text-muted)',
                              fontWeight: 'normal'
                            }}
                          >
                            {item.Unit}
                          </span>
                        </td>
                        <td
                          style={{
                            fontWeight: 900,
                            color: 'var(--brand-primary)',
                            fontSize: '18px'
                          }}
                        >
                          {maxReturnable}{' '}
                          <span
                            style={{
                              fontSize: '12px',
                              color: 'var(--text-muted)',
                              fontWeight: 'normal'
                            }}
                          >
                            {item.Unit}
                          </span>
                        </td>
                        <td>
                          <input
                            type="number"
                            step={item.QuantityType === 'kg' ? '0.01' : '1'}
                            className="pos-input"
                            style={{ textAlign: 'center', fontWeight: 900, fontSize: '20px' }}
                            value={item.QtyToReturn}
                            onChange={(e) => handleQtyChange(idx, e.target.value)}
                            disabled={isFullyReturned || bill.Status === 3}
                            placeholder={isFullyReturned ? 'DONE' : '0'}
                          />
                        </td>
                        <td
                          style={{
                            textAlign: 'right',
                            fontWeight: 900,
                            color: 'var(--action-danger)',
                            fontSize: '20px'
                          }}
                        >
                          Rs {refundLineTotal.toFixed(2)}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* FOOTER ACTIONS */}
            <div className={styles.billFooter}>
              <div style={{ flex: 1 }}>
                <label
                  style={{
                    display: 'block',
                    fontSize: '13px',
                    fontWeight: 800,
                    color: 'var(--text-muted)',
                    marginBottom: '8px',
                    textTransform: 'uppercase'
                  }}
                >
                  Return Reason / Note
                </label>
                <input
                  type="text"
                  className="pos-input"
                  value={returnReason}
                  onChange={(e) => setReturnReason(e.target.value)}
                  style={{ width: '400px' }}
                  disabled={bill.Status === 3}
                />
              </div>
              <div className={styles.summaryBox}>
                <div
                  style={{
                    fontSize: '14px',
                    color: '#b91c1c',
                    fontWeight: 800,
                    textTransform: 'uppercase'
                  }}
                >
                  Total Cash to Refund
                </div>
                <div style={{ fontSize: '42px', fontWeight: 900, color: '#dc2626' }}>
                  Rs {totalRefundAmount.toFixed(2)}
                </div>
              </div>
              <button
                className="pos-btn danger"
                style={{ minHeight: '80px', padding: '0 40px', fontSize: '20px' }}
                onClick={handleProcessReturn}
                disabled={totalRefundAmount <= 0 || bill.Status === 3}
              >
                PROCESS REFUND
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
