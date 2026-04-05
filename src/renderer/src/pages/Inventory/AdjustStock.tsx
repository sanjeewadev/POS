// src/renderer/src/pages/Inventory/AdjustStock.tsx
import React, { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Product, Category } from '../../types/models'
import styles from './AdjustStock.module.css'

export default function AdjustStock() {
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])

  // Left Sidebar State
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCatId, setSelectedCatId] = useState<number | null>(null)

  // Active Product State
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)
  const [activeBatches, setActiveBatches] = useState<any[]>([])
  const [adjustmentHistory, setAdjustmentHistory] = useState<any[]>([])

  // Form State
  const [selectedBatchId, setSelectedBatchId] = useState('')
  const [qtyToRemove, setQtyToRemove] = useState('')
  const [reason, setReason] = useState('0') // 0 = Correction, 1 = Lost
  const [note, setNote] = useState('')

  const loadBaseData = async () => {
    try {
      // @ts-ignore
      setCategories(await window.api.getCategories())
      // @ts-ignore
      setProducts(await window.api.getProducts())
    } catch (err) {
      console.error('Failed to load data', err)
    }
  }

  useEffect(() => {
    loadBaseData()
  }, [])

  const mainCategories = useMemo(() => {
    return categories.filter((c) => c.ParentId === null)
  }, [categories])

  const handleSelectProduct = async (prod: Product) => {
    setSelectedProduct(prod)
    setSelectedBatchId('')
    setQtyToRemove('')
    setReason('0')
    setNote('')

    try {
      // @ts-ignore
      const batches = await window.api.getProductBatches(prod.Id)
      const sortedBatches = batches
        .filter((b: any) => b.RemainingQuantity > 0)
        .sort(
          (a: any, b: any) =>
            new Date(b.ReceivedDate).getTime() - new Date(a.ReceivedDate).getTime()
        )

      setActiveBatches(sortedBatches)

      // @ts-ignore
      const history = await window.api.getProductAdjustments(prod.Id)
      setAdjustmentHistory(history || [])
    } catch (err) {
      console.error(err)
    }
  }

  const handleAdjustStock = async (e: React.FormEvent) => {
    e.preventDefault()

    if (!selectedProduct || !selectedBatchId) {
      Swal.fire('Missing Info', 'Select a product and a batch.', 'warning')
      return
    }

    const qty = parseFloat(qtyToRemove)
    if (isNaN(qty) || qty <= 0) {
      Swal.fire('Invalid Quantity', 'Enter a valid quantity greater than 0.', 'error')
      return
    }

    // 🚀 NEW: Flawless decimal blocking using our new Database feature!
    const isWholeNumberOnly = selectedProduct.QuantityType === 'quantity'
    if (isWholeNumberOnly && qty % 1 !== 0) {
      Swal.fire(
        'Invalid Quantity',
        `You cannot remove partial quantities (${qty}) for this item. It is marked as 'Whole Items Only'.`,
        'error'
      )
      return
    }

    const safeNote = note.trim()
    if (reason === '1' && safeNote.length < 5) {
      Swal.fire(
        'Security Requirement',
        'You must provide a clear reason/note (at least 5 characters) explaining why this item is being marked as Lost or Damaged.',
        'warning'
      )
      return
    }

    const batch = activeBatches.find((b) => b.Id.toString() === selectedBatchId)
    if (!batch) return

    if (qty > batch.RemainingQuantity) {
      Swal.fire(
        'Insufficient Stock',
        `Cannot remove ${qty}. Only ${batch.RemainingQuantity} left in this batch.`,
        'error'
      )
      return
    }

    const confirmResult = await Swal.fire({
      title: '🚨 WARNING: Permanent Action',
      text: `You are about to permanently remove ${qty} ${selectedProduct.Unit} of ${selectedProduct.Name} from the system.\n\nFinancial Loss: Rs ${(qty * batch.CostPrice).toFixed(2)}`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, remove stock!'
    })

    if (confirmResult.isConfirmed) {
      try {
        const payload = {
          ProductId: selectedProduct.Id,
          StockBatchId: parseInt(selectedBatchId),
          Quantity: qty,
          Reason: parseInt(reason),
          Note: safeNote || `Manual correction adjustment`
        }

        // @ts-ignore
        await window.api.adjustStock(payload)
        Swal.fire('Success!', 'Stock removed successfully. Financial records updated.', 'success')

        loadBaseData()
        handleSelectProduct(selectedProduct)
      } catch (err: any) {
        Swal.fire('Error', err.message || 'Error adjusting stock.', 'error')
      }
    }
  }

  const displayedProducts = useMemo(() => {
    return products.filter((p) => {
      const isInCategoryOrSub =
        selectedCatId === null
          ? true
          : p.CategoryId === selectedCatId ||
            categories.find((c) => c.Id === p.CategoryId)?.ParentId === selectedCatId

      const q = searchQuery.toLowerCase()
      const matchSearch =
        p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))

      return isInCategoryOrSub && matchSearch
    })
  }, [products, selectedCatId, searchQuery, categories])

  const totalFinancialLoss = useMemo(() => {
    return adjustmentHistory.reduce((sum, adj) => sum + adj.Quantity * adj.UnitCost, 0)
  }, [adjustmentHistory])

  const totalUnitsLost = useMemo(() => {
    return adjustmentHistory.reduce((sum, adj) => sum + adj.Quantity, 0)
  }, [adjustmentHistory])

  return (
    <div className={styles.container}>
      {/* --- LEFT SIDEBAR --- */}
      <div className={styles.leftSidebar}>
        <div className={styles.searchHeader}>
          <input
            type="text"
            className="pos-input"
            placeholder="Search products..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          <select
            className="pos-input"
            style={{ marginTop: '10px' }}
            value={selectedCatId || ''}
            onChange={(e) => setSelectedCatId(e.target.value ? parseInt(e.target.value) : null)}
          >
            <option value="">All Main Categories</option>
            {mainCategories.map((c) => (
              <option key={c.Id} value={c.Id}>
                {c.Name}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.productList}>
          {displayedProducts.map((p) => (
            <div
              key={p.Id}
              className={`${styles.productCard} ${selectedProduct?.Id === p.Id ? styles.active : ''}`}
              onClick={() => handleSelectProduct(p)}
            >
              <div>
                <div className={styles.prodName}>{p.Name}</div>
                <div className={styles.prodCode}>{p.Barcode || 'N/A'}</div>
              </div>
              <div className={`${styles.stockBadge} ${p.Quantity <= 0 ? styles.empty : ''}`}>
                {p.Quantity} {p.Unit}
              </div>
            </div>
          ))}
          {displayedProducts.length === 0 && (
            <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No products found.
            </div>
          )}
        </div>
      </div>

      {/* --- MAIN WORKSPACE --- */}
      <div className={styles.mainArea}>
        {!selectedProduct ? (
          <div className={styles.emptyPanel}>
            <h2>Select a product from the list to adjust stock.</h2>
          </div>
        ) : (
          <>
            <div className={styles.panel}>
              <div className={styles.productHeader}>
                <div>
                  <h2 className={styles.productTitle}>{selectedProduct.Name}</h2>
                  <div
                    style={{
                      fontSize: '14px',
                      color: 'var(--text-muted)',
                      marginTop: '4px',
                      fontWeight: 600
                    }}
                  >
                    SKU: {selectedProduct.Barcode || 'N/A'} |{' '}
                    {selectedProduct.QuantityType === 'kg' ? '⚖️ Weight' : '🔢 Whole Items'}
                  </div>
                </div>
                <div
                  style={{
                    textAlign: 'right',
                    background: 'var(--bg-main)',
                    padding: '10px 20px',
                    borderRadius: '8px'
                  }}
                >
                  <div
                    style={{
                      fontSize: '12px',
                      fontWeight: 800,
                      color: 'var(--text-muted)',
                      textTransform: 'uppercase'
                    }}
                  >
                    Total System Stock
                  </div>
                  <div style={{ fontSize: '28px', fontWeight: 900, color: 'var(--brand-primary)' }}>
                    {selectedProduct.Quantity}{' '}
                    <span style={{ fontSize: '16px', color: 'var(--text-muted)' }}>
                      {selectedProduct.Unit}
                    </span>
                  </div>
                </div>
              </div>

              <form onSubmit={handleAdjustStock} className={styles.formGrid}>
                <div className={styles.formGroup} style={{ gridColumn: 'span 2' }}>
                  <label>1. Select Batch to Reduce</label>
                  <select
                    className="pos-input"
                    value={selectedBatchId}
                    onChange={(e) => setSelectedBatchId(e.target.value)}
                    required
                  >
                    <option value="">-- Choose specific batch --</option>
                    {activeBatches.map((b: any) => (
                      <option key={b.Id} value={b.Id}>
                        Current Qty: {b.RemainingQuantity} | Rec:{' '}
                        {new Date(b.ReceivedDate).toLocaleDateString()} | Cost: Rs{' '}
                        {b.CostPrice.toFixed(2)}
                      </option>
                    ))}
                  </select>
                </div>

                <div className={styles.formGroup}>
                  <label>2. Qty to Remove</label>
                  <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <input
                      type="number"
                      step={selectedProduct.QuantityType === 'kg' ? '0.01' : '1'}
                      className="pos-input"
                      value={qtyToRemove}
                      onChange={(e) => setQtyToRemove(e.target.value)}
                      required
                      placeholder="0"
                    />
                    <span style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-muted)' }}>
                      {selectedProduct.Unit}
                    </span>
                  </div>
                </div>

                <div className={styles.formGroup}>
                  <label>3. Reason</label>
                  <select
                    className="pos-input"
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  >
                    <option value="0">Correction (Audit / Error)</option>
                    <option value="1">Lost / Damaged / Expired</option>
                  </select>
                </div>

                <div className={styles.formGroup} style={{ gridColumn: 'span 3' }}>
                  <label>
                    4. Explanation Note{' '}
                    {reason === '1' && (
                      <span style={{ color: 'var(--action-danger)' }}>(Required)</span>
                    )}
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    placeholder={
                      reason === '1'
                        ? 'Explain exactly how this was lost or damaged...'
                        : 'Optional note...'
                    }
                    required={reason === '1'}
                  />
                </div>

                <button
                  type="submit"
                  className="pos-btn danger"
                  disabled={!selectedBatchId || activeBatches.length === 0}
                  style={{ gridColumn: 'span 3' }}
                >
                  ⚠️ REMOVE STOCK
                </button>
              </form>
            </div>

            <div
              className={styles.panel}
              style={{ flex: 1, display: 'flex', flexDirection: 'column' }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '15px'
                }}
              >
                <h3
                  style={{
                    margin: 0,
                    fontSize: '16px',
                    color: 'var(--text-dark)',
                    textTransform: 'uppercase'
                  }}
                >
                  Adjustment & Loss History
                </h3>

                {adjustmentHistory.length > 0 && (
                  <div
                    style={{
                      display: 'flex',
                      gap: '20px',
                      background: '#fef2f2',
                      padding: '10px',
                      borderRadius: '8px',
                      border: '1px solid #fca5a5'
                    }}
                  >
                    <div style={{ textAlign: 'right' }}>
                      <div
                        style={{
                          fontSize: '11px',
                          fontWeight: 800,
                          color: 'var(--text-muted)',
                          textTransform: 'uppercase'
                        }}
                      >
                        Total Units Lost
                      </div>
                      <div style={{ fontSize: '16px', fontWeight: 900, color: 'var(--text-dark)' }}>
                        {totalUnitsLost.toFixed(2)} {selectedProduct.Unit}
                      </div>
                    </div>
                    <div
                      style={{
                        textAlign: 'right',
                        paddingLeft: '20px',
                        borderLeft: '1px solid #fca5a5'
                      }}
                    >
                      <div
                        style={{
                          fontSize: '11px',
                          fontWeight: 800,
                          color: 'var(--text-muted)',
                          textTransform: 'uppercase'
                        }}
                      >
                        Financial Loss
                      </div>
                      <div
                        style={{ fontSize: '16px', fontWeight: 900, color: 'var(--action-danger)' }}
                      >
                        Rs {totalFinancialLoss.toFixed(2)}
                      </div>
                    </div>
                  </div>
                )}
              </div>

              <div
                style={{
                  flex: 1,
                  overflowY: 'auto',
                  border: '1px solid #e2e8f0',
                  borderRadius: '8px'
                }}
              >
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                  <thead
                    style={{
                      position: 'sticky',
                      top: 0,
                      backgroundColor: 'var(--bg-main)',
                      zIndex: 1
                    }}
                  >
                    <tr>
                      <th style={{ padding: '12px', fontSize: '12px', color: 'var(--text-muted)' }}>
                        DATE
                      </th>
                      <th style={{ padding: '12px', fontSize: '12px', color: 'var(--text-muted)' }}>
                        TYPE
                      </th>
                      <th
                        style={{
                          padding: '12px',
                          fontSize: '12px',
                          color: 'var(--text-muted)',
                          textAlign: 'center'
                        }}
                      >
                        QTY REMOVED
                      </th>
                      <th
                        style={{
                          padding: '12px',
                          fontSize: '12px',
                          color: 'var(--text-muted)',
                          textAlign: 'right'
                        }}
                      >
                        FINANCIAL VALUE
                      </th>
                      <th style={{ padding: '12px', fontSize: '12px', color: 'var(--text-muted)' }}>
                        NOTE
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {adjustmentHistory.length === 0 ? (
                      <tr>
                        <td
                          colSpan={5}
                          style={{
                            textAlign: 'center',
                            padding: '40px',
                            color: 'var(--text-muted)',
                            fontWeight: 600
                          }}
                        >
                          No manual adjustments recorded for this product.
                        </td>
                      </tr>
                    ) : (
                      adjustmentHistory.map((adj: any) => (
                        <tr key={adj.Id} style={{ borderBottom: '1px solid #e2e8f0' }}>
                          <td
                            style={{
                              padding: '12px',
                              color: 'var(--text-muted)',
                              fontSize: '13px'
                            }}
                          >
                            {new Date(adj.Date).toLocaleString()}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                fontSize: '11px',
                                fontWeight: 800,
                                textTransform: 'uppercase',
                                backgroundColor: adj.Reason === 1 ? '#fef2f2' : '#fffbeb',
                                color: adj.Reason === 1 ? '#dc2626' : '#d97706'
                              }}
                            >
                              {adj.Reason === 0 ? 'Correction' : 'Lost / Damaged'}
                            </span>
                          </td>
                          <td
                            style={{
                              padding: '12px',
                              fontWeight: 900,
                              color: 'var(--text-dark)',
                              textAlign: 'center'
                            }}
                          >
                            - {adj.Quantity}{' '}
                            <span
                              style={{
                                fontSize: '11px',
                                color: 'var(--text-muted)',
                                fontWeight: 'normal'
                              }}
                            >
                              {selectedProduct.Unit}
                            </span>
                          </td>
                          <td
                            style={{
                              padding: '12px',
                              fontWeight: 800,
                              color: 'var(--action-danger)',
                              textAlign: 'right'
                            }}
                          >
                            Rs {(adj.Quantity * adj.UnitCost).toFixed(2)}
                          </td>
                          <td
                            style={{
                              padding: '12px',
                              color: 'var(--text-muted)',
                              fontSize: '13px'
                            }}
                          >
                            {adj.Note}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
