// src/renderer/src/pages/Inventory/ProductCatalog.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Category, Product } from '../../types/models'
import styles from './ProductCatalog.module.css'

export default function ProductCatalog() {
  const [categories, setCategories] = useState<Category[]>([])
  const [products, setProducts] = useState<Product[]>([])

  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCatId, setSelectedCatId] = useState<number | null>(null)
  const [expandedFolders, setExpandedFolders] = useState<Set<number>>(new Set())

  const [viewingProduct, setViewingProduct] = useState<Product | null>(null)
  const [productBatches, setProductBatches] = useState<any[]>([])

  const [quickAddProduct, setQuickAddProduct] = useState<Product | null>(null)
  const [quickAddQty, setQuickAddQty] = useState('')
  const [quickAddCost, setQuickAddCost] = useState('')
  const [quickAddSell, setQuickAddSell] = useState('')
  const [quickAddDiscount, setQuickAddDiscount] = useState('')
  const [quickAddDiscountType, setQuickAddDiscountType] = useState('percentage')

  const [editingBatch, setEditingBatch] = useState<any>(null)
  const [editBatchSell, setEditBatchSell] = useState('')
  const [editBatchDiscount, setEditBatchDiscount] = useState('')
  const [editBatchDiscountType, setEditBatchDiscountType] = useState('percentage')

  const loadData = async () => {
    try {
      // @ts-ignore
      const catData = await window.api.getCategories()
      // @ts-ignore
      const prodData = await window.api.getProducts()
      setCategories(catData)
      setProducts(prodData)
    } catch (err: any) {
      Swal.fire('Error', 'Failed to load catalog data: ' + err.message, 'error')
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  const handleViewProduct = async (product: Product) => {
    setViewingProduct(product)
    try {
      // @ts-ignore
      const batches = await window.api.getProductBatches(product.Id)
      const activeBatches = (batches || [])
        .filter((b: any) => b.RemainingQuantity > 0)
        .sort(
          (a: any, b: any) =>
            new Date(b.ReceivedDate).getTime() - new Date(a.ReceivedDate).getTime()
        )
      setProductBatches(activeBatches)
    } catch (err: any) {
      Swal.fire('Error', 'Failed to load product batches: ' + err.message, 'error')
      setProductBatches([])
    }
  }

  const handleOpenQuickAdd = (product: Product) => {
    setQuickAddProduct(product)
    setQuickAddQty('')
    setQuickAddCost(product.BuyingPrice ? product.BuyingPrice.toString() : '')
    setQuickAddSell(product.SellingPrice ? product.SellingPrice.toString() : '')
    setQuickAddDiscount(product.DiscountLimit ? product.DiscountLimit.toString() : '0')
    setQuickAddDiscountType('percentage')
  }

  const submitQuickAdd = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!quickAddProduct) return

    const qty = parseFloat(quickAddQty)
    const cost = parseFloat(quickAddCost)
    const sell = parseFloat(quickAddSell)
    const disc = parseFloat(quickAddDiscount) || 0

    if (isNaN(qty) || qty <= 0 || isNaN(cost) || cost < 0 || isNaN(sell) || sell <= 0) {
      return Swal.fire(
        'Invalid Input',
        'Please enter valid numbers for quantity and prices.',
        'error'
      )
    }
    if (sell < cost) {
      return Swal.fire('Warning', 'Selling Price cannot be lower than Buying Cost!', 'warning')
    }
    if (quickAddDiscountType === 'percentage' && (disc < 0 || disc > 100)) {
      return Swal.fire(
        'Invalid Discount',
        'Discount percentage must be between 0 and 100.',
        'warning'
      )
    }
    if (quickAddDiscountType === 'amount' && disc > sell) {
      return Swal.fire(
        'Invalid Discount',
        'Discount amount cannot be greater than the selling price!',
        'warning'
      )
    }
    if (quickAddProduct.QuantityType === 'quantity' && qty % 1 !== 0) {
      return Swal.fire(
        'Error',
        `Cannot add partial amounts. ${quickAddProduct.Name} is set to 'Whole Items Only'.`,
        'error'
      )
    }

    try {
      const payload = {
        ProductId: quickAddProduct.Id,
        Quantity: qty,
        UnitCost: cost,
        SellingPrice: sell,
        Discount: disc,
        DiscountType: quickAddDiscountType,
        Date: new Date().toISOString()
      }

      // @ts-ignore
      await window.api.receiveStock(payload)
      Swal.fire({
        title: 'Stock Added!',
        text: `Successfully added ${qty} ${quickAddProduct.Unit}.`,
        icon: 'success',
        timer: 1500,
        showConfirmButton: false
      })
      setQuickAddProduct(null)
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Failed to add stock: ' + err.message, 'error')
    }
  }

  const handleOpenEditBatch = (batch: any) => {
    setEditingBatch(batch)
    setEditBatchSell((batch.SellingPrice || 0).toString())
    setEditBatchDiscount((batch.Discount || 0).toString())
    setEditBatchDiscountType(batch.DiscountType || 'percentage')
  }

  const submitBatchEdit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editingBatch) return

    const sell = parseFloat(editBatchSell)
    const disc = parseFloat(editBatchDiscount) || 0

    if (isNaN(sell) || sell <= 0)
      return Swal.fire('Invalid Input', 'Please enter a valid selling price.', 'error')
    if (editBatchDiscountType === 'percentage' && (disc < 0 || disc > 100))
      return Swal.fire('Invalid Discount', 'Percentage must be 0-100.', 'warning')
    if (editBatchDiscountType === 'amount' && disc > sell)
      return Swal.fire(
        'Invalid Discount',
        'Discount amount cannot exceed the selling price!',
        'warning'
      )

    try {
      // @ts-ignore
      await window.api.updateBatchPricing({
        BatchId: editingBatch.Id,
        SellingPrice: sell,
        Discount: disc,
        DiscountType: editBatchDiscountType
      })

      Swal.fire({
        title: 'Pricing Updated!',
        icon: 'success',
        timer: 1200,
        showConfirmButton: false
      })
      setEditingBatch(null)
      if (viewingProduct) handleViewProduct(viewingProduct)
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Failed to update pricing: ' + err.message, 'error')
    }
  }

  const getRelevantCategoryIds = (startId: number | null): number[] => {
    if (startId === null) return []
    let ids = [startId]
    const children = categories.filter((c) => c.ParentId === startId)
    children.forEach((c) => {
      ids = [...ids, ...getRelevantCategoryIds(c.Id)]
    })
    return ids
  }

  const treeContent = useMemo(() => {
    const renderTree = (parentId: number | null, depth: number = 0) => {
      const children = categories.filter((c) => c.ParentId === parentId)
      if (children.length === 0) return null

      return children.map((cat) => {
        const hasChildren = categories.some((c) => c.ParentId === cat.Id)
        const isExpanded = expandedFolders.has(cat.Id)
        const isActive = selectedCatId === cat.Id

        return (
          <div key={cat.Id}>
            <div
              className={`${styles.treeNode} ${isActive ? styles.active : ''}`}
              style={{ paddingLeft: `${depth * 15 + 10}px` }}
              onClick={() => {
                setSelectedCatId(cat.Id)
                setSearchQuery('')
              }}
            >
              <span
                className={styles.expandIcon}
                onClick={(e) => {
                  if (hasChildren) {
                    e.stopPropagation()
                    const newExpanded = new Set(expandedFolders)
                    if (newExpanded.has(cat.Id)) newExpanded.delete(cat.Id)
                    else newExpanded.add(cat.Id)
                    setExpandedFolders(newExpanded)
                  }
                }}
              >
                {hasChildren ? (isExpanded ? '▼' : '▶') : ''}
              </span>
              <span className={styles.folderIcon}>{isExpanded ? '📂' : '📁'}</span>
              {cat.Name}
            </div>
            {isExpanded && renderTree(cat.Id, depth + 1)}
          </div>
        )
      })
    }
    return renderTree(null)
  }, [categories, expandedFolders, selectedCatId])

  const displayedProducts = useMemo(() => {
    if (searchQuery) {
      const q = searchQuery.toLowerCase()
      return products.filter(
        (p) =>
          p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))
      )
    }
    if (selectedCatId === null) return products
    const relevantIds = getRelevantCategoryIds(selectedCatId)
    return products.filter((p) => relevantIds.includes(p.CategoryId))
  }, [products, selectedCatId, searchQuery, categories])

  const getCatName = (id: number | null) => categories.find((c) => c.Id === id)?.Name || 'N/A'

  return (
    <div className={styles.container}>
      {/* ── FOLDER SIDEBAR ── */}
      <div className={styles.leftPanel}>
        <div className={styles.panelHeader}>Filter By Folder</div>
        <div className={styles.treeContainer}>
          <div
            className={`${styles.treeNode} ${selectedCatId === null && !searchQuery ? styles.active : ''}`}
            onClick={() => {
              setSelectedCatId(null)
              setSearchQuery('')
            }}
          >
            <span className={styles.expandIcon}>•</span>
            <span className={styles.folderIcon}>📦</span>
            All Products
          </div>
          {treeContent}
        </div>
      </div>

      {/* ── MAIN TABLE ── */}
      <div className={styles.panel}>
        <div className={styles.headerRow}>
          <h2 className="pos-page-title">LIVE CATALOG</h2>
          <input
            type="text"
            className={`pos-input ${styles.searchInput}`}
            placeholder="Search Name or Barcode Scanner..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value)
              if (e.target.value) setSelectedCatId(null)
            }}
          />
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th className={styles.colCode}>CODE</th>
                <th className={styles.colName}>NAME</th>
                <th className={styles.colPrice}>SELL PRICE</th>
                <th className={styles.colStock}>IN STOCK</th>
                <th className={styles.colActions}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {displayedProducts.length === 0 ? (
                <tr>
                  <td colSpan={5} className={styles.emptyState}>
                    {searchQuery
                      ? 'No products match your search.'
                      : 'No products found in this folder.'}
                  </td>
                </tr>
              ) : (
                displayedProducts.map((product) => (
                  <tr key={product.Id}>
                    <td className={styles.barcodeCell}>{product.Barcode}</td>
                    <td>
                      {/* 🚀 THE FIX: Text Truncation wrappers for long names */}
                      <div className={styles.productNameWrapper}>
                        <div className={styles.productName} title={product.Name}>
                          {product.Name}
                        </div>
                        <div className={styles.categoryName}>{getCatName(product.CategoryId)}</div>
                      </div>
                    </td>
                    <td className={styles.priceSuccess}>
                      Rs {(product.SellingPrice || 0).toFixed(2)}
                    </td>
                    <td className={product.Quantity <= 0 ? styles.qtyDanger : styles.qtyPrimary}>
                      {product.Quantity || 0}{' '}
                      <span className={styles.unitSpan}>{product.Unit}</span>
                    </td>
                    <td className={styles.actionCell}>
                      <button
                        className={`pos-btn-sm neutral ${styles.actionBtn}`}
                        onClick={() => handleViewProduct(product)}
                      >
                        INFO
                      </button>
                      <button
                        className={`pos-btn-sm success ${styles.actionBtn}`}
                        onClick={() => handleOpenQuickAdd(product)}
                      >
                        STOCK ADD
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── QUICK ADD MODAL ── */}
      {quickAddProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={`${styles.modalBoxView} ${styles.modalQuickAdd}`}>
            <div className={styles.modalHeader}>
              <h2 className="pos-page-title">⚡ Fast Stock Receive</h2>
              <button
                className={`pos-btn neutral ${styles.actionBtn}`}
                onClick={() => setQuickAddProduct(null)}
              >
                Close
              </button>
            </div>

            <form onSubmit={submitQuickAdd} className={styles.modalBody}>
              <div className={styles.infoBox}>
                <h3 className={styles.infoBoxTitle}>{quickAddProduct.Name}</h3>
                <div className={styles.infoBoxText}>
                  CURRENT STOCK:{' '}
                  <span className={styles.infoBoxHighlight}>
                    {quickAddProduct.Quantity} {quickAddProduct.Unit}
                  </span>
                </div>
              </div>

              <div className={styles.formRow}>
                <div className={styles.formCol}>
                  <label className={styles.inputLabel}>Qty Received</label>
                  <div className={styles.inputGroup}>
                    <input
                      type="number"
                      step={quickAddProduct.QuantityType === 'kg' ? '0.01' : '1'}
                      className="pos-input"
                      value={quickAddQty}
                      onChange={(e) => setQuickAddQty(e.target.value)}
                      required
                      autoFocus
                    />
                    <span className={styles.inputSuffix}>{quickAddProduct.Unit}</span>
                  </div>
                </div>

                <div className={styles.formCol}>
                  <label className={styles.inputLabel}>Supplier Unit Cost</label>
                  <div className={styles.inputGroup}>
                    <span className={styles.inputPrefix}>Rs</span>
                    <input
                      type="number"
                      step="0.01"
                      className="pos-input"
                      value={quickAddCost}
                      onChange={(e) => setQuickAddCost(e.target.value)}
                      required
                    />
                  </div>
                </div>
              </div>

              <div className={`${styles.formRow} ${styles.pricingBox}`}>
                <div className={styles.formCol}>
                  <label className={`${styles.inputLabel} ${styles.inputLabelSuccess}`}>
                    New Selling Price
                  </label>
                  <div className={styles.inputGroup}>
                    <span className={styles.inputPrefix}>Rs</span>
                    <input
                      type="number"
                      step="0.01"
                      className={`pos-input ${styles.successInput}`}
                      value={quickAddSell}
                      onChange={(e) => setQuickAddSell(e.target.value)}
                      required
                    />
                  </div>
                </div>

                <div className={styles.formCol}>
                  <label className={`${styles.inputLabel} ${styles.inputLabelWarning}`}>
                    Max Allowable Discount
                  </label>
                  <div className={styles.inputGroupSm}>
                    <select
                      className={`pos-input ${styles.discountSelect}`}
                      value={quickAddDiscountType}
                      onChange={(e) => setQuickAddDiscountType(e.target.value)}
                    >
                      <option value="percentage">%</option>
                      <option value="amount">Rs</option>
                    </select>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      className="pos-input"
                      value={quickAddDiscount}
                      onChange={(e) => setQuickAddDiscount(e.target.value)}
                      placeholder={quickAddDiscountType === 'percentage' ? 'e.g. 10' : 'e.g. 50.00'}
                    />
                  </div>
                </div>
              </div>

              <button type="submit" className={`pos-btn success ${styles.submitBtn}`}>
                CONFIRM & ADD BATCH TO INVENTORY
              </button>
            </form>
          </div>
        </div>
      )}

      {/* ── INFO MODAL (BATCH HISTORY) ── */}
      {viewingProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={`${styles.modalBoxView} ${styles.modalInfo}`}>
            <div className={styles.modalHeader}>
              <div>
                <h2 className="pos-page-title">{viewingProduct.Name}</h2>
                <div className={styles.modalSubtitle}>
                  Category: {getCatName(viewingProduct.CategoryId)} | Base Unit:{' '}
                  {viewingProduct.Unit} | Code: {viewingProduct.Barcode || 'N/A'}
                </div>
              </div>
              <div className={styles.totalStockBox}>
                <div className={styles.totalStockLabel}>Total Stock</div>
                <div className={styles.totalStockValue}>
                  {viewingProduct.Quantity || 0}{' '}
                  <span className={styles.totalStockUnit}>{viewingProduct.Unit}</span>
                </div>
              </div>
            </div>

            <div className={styles.modalBody}>
              <h3 className={styles.tableTitle}>Active Inventory Batches (Pricing History)</h3>

              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>DATE</th>
                      <th>QTY (Cur/Orig)</th>
                      <th>COST</th>
                      <th>BASE SELL</th>
                      <th>DISC (%)</th>
                      <th>DISC (Rs)</th>
                      <th>MIN PRICE</th>
                      <th className={styles.textRight}>ACTIONS</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productBatches.length === 0 ? (
                      <tr>
                        <td colSpan={8} className={styles.emptyState}>
                          No active batches found. Receive GRN or Quick Add stock to see data here.
                        </td>
                      </tr>
                    ) : (
                      productBatches.map((batch, idx) => {
                        const basePrice = batch.SellingPrice || 0
                        const rawDisc = batch.Discount || 0
                        const isPct = batch.DiscountType === 'percentage' || !batch.DiscountType

                        const discountPct = isPct
                          ? rawDisc
                          : basePrice > 0
                            ? (rawDisc / basePrice) * 100
                            : 0
                        const discountAmt = isPct ? basePrice * (rawDisc / 100) : rawDisc
                        const minPrice = basePrice - discountAmt

                        return (
                          <tr key={idx}>
                            <td className={styles.cellDate}>
                              {new Date(batch.ReceivedDate).toLocaleDateString()}
                            </td>
                            <td>
                              <span className={styles.cellDarkBold}>{batch.RemainingQuantity}</span>
                              <span className={styles.qtySeparator}>/ {batch.InitialQuantity}</span>
                            </td>
                            <td className={styles.cellMuted}>Rs {batch.CostPrice.toFixed(2)}</td>
                            <td className={styles.cellDarkBoldMd}>Rs {basePrice.toFixed(2)}</td>
                            <td className={styles.cellWarning}>{discountPct.toFixed(1)}%</td>
                            <td className={styles.cellWarning}>Rs {discountAmt.toFixed(2)}</td>
                            <td className={styles.cellDanger}>
                              Rs {Math.max(0, minPrice).toFixed(2)}
                            </td>
                            <td className={styles.textRight}>
                              <button
                                className={`pos-btn-sm warning ${styles.actionBtnSm}`}
                                onClick={() => handleOpenEditBatch(batch)}
                              >
                                EDIT PRICE
                              </button>
                            </td>
                          </tr>
                        )
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className="pos-btn neutral" onClick={() => setViewingProduct(null)}>
                CLOSE WINDOW
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── NEW MODAL: EDIT BATCH PRICING ── */}
      {editingBatch !== null && (
        <div className={`${styles.modalOverlay} ${styles.modalOverlayHigh}`}>
          <div className={`${styles.modalBoxView} ${styles.modalEditPrice}`}>
            <div className={styles.modalHeader}>
              <h2 className="pos-page-title">✏️ Update Batch Pricing</h2>
              <button
                className={`pos-btn neutral ${styles.actionBtn}`}
                onClick={() => setEditingBatch(null)}
              >
                Close
              </button>
            </div>

            <form onSubmit={submitBatchEdit} className={styles.modalBody}>
              <div className={styles.lockedCostBox}>
                <div className={styles.inputLabel}>Locked Buying Cost</div>
                <div className={styles.cellDarkBoldLg}>
                  Rs {(editingBatch.CostPrice || 0).toFixed(2)}
                </div>
                <div className={styles.lockedCostNote}>
                  * Cost price is permanently locked to protect historical profit reports.
                </div>
              </div>

              <div className={styles.formRowCol}>
                <label className={`${styles.inputLabel} ${styles.inputLabelSuccess}`}>
                  New Base Selling Price
                </label>
                <div className={styles.inputGroup}>
                  <span className={styles.inputPrefix}>Rs</span>
                  <input
                    type="number"
                    step="0.01"
                    className={`pos-input ${styles.successInput}`}
                    value={editBatchSell}
                    onChange={(e) => setEditBatchSell(e.target.value)}
                    required
                    autoFocus
                  />
                </div>
              </div>

              <div className={styles.formRowCol}>
                <label className={`${styles.inputLabel} ${styles.inputLabelWarning}`}>
                  New Max Allowable Discount
                </label>
                <div className={styles.inputGroupSm}>
                  <select
                    className={`pos-input ${styles.discountSelect}`}
                    value={editBatchDiscountType}
                    onChange={(e) => setEditBatchDiscountType(e.target.value)}
                  >
                    <option value="percentage">%</option>
                    <option value="amount">Rs</option>
                  </select>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    className="pos-input"
                    value={editBatchDiscount}
                    onChange={(e) => setEditBatchDiscount(e.target.value)}
                  />
                </div>
              </div>

              <button type="submit" className={`pos-btn warning ${styles.submitBtn}`}>
                UPDATE PRICING NOW
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
