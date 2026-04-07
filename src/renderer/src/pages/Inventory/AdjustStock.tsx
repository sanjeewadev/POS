// src/renderer/src/pages/Inventory/AdjustStock.tsx
import React, { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Product, Category } from '../../types/models'
import styles from './AdjustStock.module.css'

export default function AdjustStock() {
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])

  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCatId, setSelectedCatId] = useState<number | null>(null)

  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)
  const [activeBatches, setActiveBatches] = useState<any[]>([])
  const [adjustmentHistory, setAdjustmentHistory] = useState<any[]>([])

  const [selectedBatchId, setSelectedBatchId] = useState('')
  const [qtyToRemove, setQtyToRemove] = useState('')
  const [reason, setReason] = useState('0')
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
            className={`pos-input ${styles.categorySelect}`}
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
              <div className={styles.productCardContent}>
                <div className={styles.prodName}>{p.Name}</div>
                <div className={styles.prodCode}>{p.Barcode || 'N/A'}</div>
              </div>
              <div className={`${styles.stockBadge} ${p.Quantity <= 0 ? styles.empty : ''}`}>
                {p.Quantity} {p.Unit}
              </div>
            </div>
          ))}
          {displayedProducts.length === 0 && (
            <div className={styles.emptyProductList}>No products found.</div>
          )}
        </div>
      </div>

      {/* --- MAIN WORKSPACE --- */}
      <div className={styles.mainArea}>
        {!selectedProduct ? (
          <div className={styles.emptyPanel}>
            <h2 className="pos-page-title">Select a product to adjust stock</h2>
          </div>
        ) : (
          <>
            {/* Action Panel */}
            <div className={styles.panel}>
              <div className={styles.productHeader}>
                <div>
                  <h2 className="pos-page-title">{selectedProduct.Name}</h2>
                  <div className={styles.productMeta}>
                    SKU: {selectedProduct.Barcode || 'N/A'} |{' '}
                    {selectedProduct.QuantityType === 'kg' ? 'Weight' : 'Whole Items'}
                  </div>
                </div>
                <div className={styles.totalStockBox}>
                  <div className={styles.totalStockLabel}>Total System Stock</div>
                  <div className={styles.totalStockValue}>
                    {selectedProduct.Quantity}{' '}
                    <span className={styles.totalStockUnit}>{selectedProduct.Unit}</span>
                  </div>
                </div>
              </div>

              <form onSubmit={handleAdjustStock} className={styles.formGrid}>
                <div className={`${styles.formGroup} ${styles.colSpan2}`}>
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
                  <div className={styles.inputGroup}>
                    <input
                      type="number"
                      step={selectedProduct.QuantityType === 'kg' ? '0.01' : '1'}
                      className="pos-input"
                      value={qtyToRemove}
                      onChange={(e) => setQtyToRemove(e.target.value)}
                      required
                      placeholder="0"
                    />
                    <span className={styles.unitSuffix}>{selectedProduct.Unit}</span>
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

                <div className={`${styles.formGroup} ${styles.colSpan3}`}>
                  <label>
                    4. Explanation Note{' '}
                    {reason === '1' && <span className={styles.requiredMark}>(Required)</span>}
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
                  className={`pos-btn danger ${styles.colSpan3}`}
                  disabled={!selectedBatchId || activeBatches.length === 0}
                >
                  ⚠️ REMOVE STOCK
                </button>
              </form>
            </div>

            {/* History Panel */}
            <div className={`${styles.panel} ${styles.historyPanel}`}>
              <div className={styles.historyHeader}>
                <h3 className={styles.historyTitle}>Adjustment & Loss History</h3>

                {adjustmentHistory.length > 0 && (
                  <div className={styles.kpiBox}>
                    <div className={styles.kpiItem}>
                      <div className={styles.kpiLabel}>Total Units Lost</div>
                      <div className={styles.kpiValueDark}>
                        {totalUnitsLost.toFixed(2)} {selectedProduct.Unit}
                      </div>
                    </div>
                    <div className={styles.kpiDivider}>
                      <div className={styles.kpiLabel}>Financial Loss</div>
                      <div className={styles.kpiValueDanger}>
                        Rs {totalFinancialLoss.toFixed(2)}
                      </div>
                    </div>
                  </div>
                )}
              </div>

              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th className={styles.colDate}>DATE</th>
                      <th className={styles.colType}>TYPE</th>
                      <th className={styles.colQtyCenter}>QTY REMOVED</th>
                      <th className={styles.colFinancial}>FINANCIAL VALUE</th>
                      <th className={styles.colNote}>NOTE</th>
                    </tr>
                  </thead>
                  <tbody>
                    {adjustmentHistory.length === 0 ? (
                      <tr>
                        <td colSpan={5} className={styles.emptyTableState}>
                          No manual adjustments recorded for this product.
                        </td>
                      </tr>
                    ) : (
                      adjustmentHistory.map((adj: any) => (
                        <tr key={adj.Id}>
                          <td className={styles.cellDate}>{new Date(adj.Date).toLocaleString()}</td>
                          <td>
                            <span
                              className={`${styles.typeBadge} ${adj.Reason === 1 ? styles.typeDanger : styles.typeWarning}`}
                            >
                              {adj.Reason === 0 ? 'Correction' : 'Lost / Damaged'}
                            </span>
                          </td>
                          <td className={styles.cellQtyDarkCenter}>
                            - {adj.Quantity}{' '}
                            <span className={styles.unitSpanSm}>{selectedProduct.Unit}</span>
                          </td>
                          <td className={styles.cellFinancialDanger}>
                            Rs {(adj.Quantity * adj.UnitCost).toFixed(2)}
                          </td>
                          <td className={styles.cellNote}>{adj.Note}</td>
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
