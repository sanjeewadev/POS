// src/renderer/src/pages/Returns/ReturnsCenter.tsx
import { useState, useMemo, useRef, useEffect } from 'react'
import Swal from 'sweetalert2'
import {
  RiArrowGoBackLine,
  RiSearch2Line,
  RiBillLine,
  RiErrorWarningLine,
  RiRefund2Line,
  RiInformationLine,
  RiCheckboxCircleLine
} from 'react-icons/ri'
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
  QuantityType: string
}

export default function ReturnsCenter() {
  const [searchQuery, setSearchQuery] = useState('')
  const [loading, setLoading] = useState(false)
  const [bill, setBill] = useState<any | null>(null)
  const [items, setItems] = useState<ReturnItem[]>([])
  const [returnReason, setReturnReason] = useState('Customer Changed Mind')

  const searchInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    searchInputRef.current?.focus()
  }, [])

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
        Swal.fire('Not Found', 'Invoice not found. Check the receipt number.', 'error')
        setLoading(false)
        return
      }
      setBill(result.transaction)
      const mappedItems = result.items.map((item: any) => ({ ...item, QtyToReturn: '' }))
      setItems(mappedItems)
    } catch (err: any) {
      Swal.fire('Error', 'Error searching for bill: ' + err.message, 'error')
    } finally {
      setLoading(false)
    }
  }

  const handleQtyChange = (index: number, val: string) => {
    const newItems = [...items]
    const item = newItems[index]
    const maxReturnable = item.OriginalQty - item.ReturnedQty

    if (val === '' || /^\d*\.?\d*$/.test(val)) {
      const numVal = parseFloat(val) || 0
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

  const totalRefundAmount = useMemo(() => {
    let total = 0
    items.forEach((item) => {
      const qty = parseFloat(item.QtyToReturn) || 0
      total += qty * item.UnitPrice
    })
    return total
  }, [items])

  const handleProcessReturn = async () => {
    const itemsToReturn = items.filter((item) => (parseFloat(item.QtyToReturn) || 0) > 0)
    if (itemsToReturn.length === 0) {
      return Swal.fire('Missing Information', 'Enter a quantity to return.', 'warning')
    }

    const safeReason = returnReason.trim() || 'Manual Return'
    let confirmMessage = `<div style="text-align: left;"><strong>Confirming stock return:</strong><ul style="margin-top: 10px;">`
    itemsToReturn.forEach((item) => {
      confirmMessage += `<li>${item.QtyToReturn} ${item.Unit} of <b>${item.ProductName}</b></li>`
    })
    confirmMessage += `</ul><div style="background: #fff1f2; border: 2px solid #fecaca; color: #e11d48; padding: 15px; border-radius: 8px; margin-top: 15px; font-size: 18px; font-weight: 900; text-align: center;">Refund to Customer:<br/>Rs ${totalRefundAmount.toFixed(2)}</div></div>`

    const confirmResult = await Swal.fire({
      title: 'Process Return?',
      html: confirmMessage,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#e11d48',
      confirmButtonText: 'Yes, Process & Refund'
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
        Swal.fire('Success', '✅ Return processed!', 'success')
        setSearchQuery(bill.ReceiptId)
        handleSearch(new Event('submit') as any)
      } catch (err: any) {
        Swal.fire('Error', err.message || 'Error processing return.', 'error')
      }
    }
  }

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <div className={styles.headerTitle}>
            <RiArrowGoBackLine size={24} color="var(--action-danger)" />
            <h2 className={styles.pageTitle}>Returns & Refunds Center</h2>
          </div>

          <form onSubmit={handleSearch} className={styles.searchForm}>
            <div className={styles.searchWrapper}>
              <RiSearch2Line className={styles.searchIcon} />
              <input
                ref={searchInputRef}
                type="text"
                className="pos-input"
                placeholder="Enter Receipt ID (INV-XXXX)..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <button
              type="submit"
              className="pos-btn success"
              disabled={loading}
              style={{ minHeight: '48px', padding: '0 24px' }}
            >
              {loading ? '...' : 'FIND BILL'}
            </button>
          </form>
        </div>

        <div className={styles.panelBody}>
          {!bill ? (
            <div className={styles.emptyState}>
              <RiBillLine size={64} color="#e2e8f0" />
              <h3>No Bill Selected</h3>
              <p>Search for a valid receipt ID to start the return process.</p>
            </div>
          ) : (
            <div className={styles.billWorkspace}>
              <div className={styles.billSummaryCard}>
                <div className={styles.summaryInfo}>
                  <div className={styles.receiptLabel}>Active Receipt</div>
                  <div className={styles.receiptId}>{bill.ReceiptId}</div>
                  <div className={styles.receiptMeta}>
                    <RiInformationLine /> {new Date(bill.TransactionDate).toLocaleString()} |{' '}
                    {bill.CustomerName || 'Walk-in Customer'}
                  </div>
                </div>
                <div className={styles.totalBlock}>
                  <div className={styles.totalLabel}>Bill Amount</div>
                  <div className={styles.totalValue}>Rs {bill.TotalAmount.toFixed(2)}</div>
                </div>
              </div>

              {bill.Status === 3 && (
                <div className={styles.voidBanner}>
                  <RiErrorWarningLine size={20} />
                  THIS RECEIPT IS VOIDED. NO FURTHER RETURNS ALLOWED.
                </div>
              )}

              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>PRODUCT NAME</th>
                      <th>SOLD PRICE</th>
                      <th style={{ textAlign: 'center' }}>BOUGHT</th>
                      <th style={{ textAlign: 'center' }}>RETURNED</th>
                      <th style={{ textAlign: 'center' }}>MAX ALLOWED</th>
                      <th style={{ width: '180px' }}>RETURN QTY</th>
                      <th style={{ textAlign: 'right' }}>REFUND (Rs)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item, idx) => {
                      const maxReturnable = item.OriginalQty - item.ReturnedQty
                      const isDone = maxReturnable <= 0
                      return (
                        <tr key={idx} className={isDone ? styles.rowDone : ''}>
                          <td style={{ fontWeight: 700 }}>{item.ProductName}</td>
                          <td style={{ fontWeight: 700, color: 'var(--action-success)' }}>
                            Rs {item.UnitPrice.toFixed(2)}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            {item.OriginalQty} <small>{item.Unit}</small>
                          </td>
                          <td
                            style={{
                              textAlign: 'center',
                              color: 'var(--action-danger)',
                              fontWeight: 700
                            }}
                          >
                            {item.ReturnedQty}
                          </td>
                          <td
                            style={{
                              textAlign: 'center',
                              fontWeight: 800,
                              color: 'var(--brand-primary)'
                            }}
                          >
                            {maxReturnable}
                          </td>
                          <td>
                            <input
                              type="number"
                              className="pos-input"
                              style={{ textAlign: 'center', fontWeight: 900, height: '40px' }}
                              value={item.QtyToReturn}
                              onChange={(e) => handleQtyChange(idx, e.target.value)}
                              disabled={isDone || bill.Status === 3}
                              placeholder="0"
                            />
                          </td>
                          <td
                            style={{
                              textAlign: 'right',
                              fontWeight: 900,
                              color: 'var(--action-danger)'
                            }}
                          >
                            Rs {(parseFloat(item.QtyToReturn || '0') * item.UnitPrice).toFixed(2)}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>

              <div className={styles.footerActions}>
                <div className={styles.reasonStack}>
                  <label>RETURN REASON / NOTE</label>
                  <input
                    type="text"
                    className="pos-input"
                    value={returnReason}
                    onChange={(e) => setReturnReason(e.target.value)}
                    disabled={bill.Status === 3}
                  />
                </div>
                <div className={styles.refundPanel}>
                  <div className={styles.refundLabel}>TOTAL REFUND DUE</div>
                  <div className={styles.refundValue}>Rs {totalRefundAmount.toFixed(2)}</div>
                </div>
                <button
                  className="pos-btn danger"
                  style={{ minHeight: '64px', padding: '0 32px', gap: '10px' }}
                  onClick={handleProcessReturn}
                  disabled={totalRefundAmount <= 0 || bill.Status === 3}
                >
                  <RiRefund2Line size={24} /> PROCESS REFUND
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
