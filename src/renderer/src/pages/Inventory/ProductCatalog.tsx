// src/renderer/src/pages/Inventory/ProductCatalog.tsx
import { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Category, Product } from '../../types/models'
import styles from './ProductCatalog.module.css'

export default function ProductCatalog() {
  const [categories, setCategories] = useState<Category[]>([])
  const [products, setProducts] = useState<Product[]>([])

  // Filtering State
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCatId, setSelectedCatId] = useState<number | null>(null)
  const [expandedFolders, setExpandedFolders] = useState<Set<number>>(new Set())

  // Modal State
  const [viewingProduct, setViewingProduct] = useState<Product | null>(null)
  const [productBatches, setProductBatches] = useState<any[]>([])

  // 🚀 UPGRADED: Quick Add State (Now includes Selling Price & Discount)
  const [quickAddProduct, setQuickAddProduct] = useState<Product | null>(null)
  const [quickAddQty, setQuickAddQty] = useState('')
  const [quickAddCost, setQuickAddCost] = useState('')
  const [quickAddSell, setQuickAddSell] = useState('')
  const [quickAddDiscount, setQuickAddDiscount] = useState('')

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

  // --- ACTIONS: VIEW BATCHES ---
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

  // --- 🚀 UPGRADED ACTIONS: QUICK ADD STOCK ---
  const handleOpenQuickAdd = (product: Product) => {
    setQuickAddProduct(product)
    setQuickAddQty('')
    // Pre-fill fields with current data to save time!
    setQuickAddCost(product.BuyingPrice ? product.BuyingPrice.toString() : '')
    setQuickAddSell(product.SellingPrice ? product.SellingPrice.toString() : '')
    setQuickAddDiscount(product.DiscountLimit ? product.DiscountLimit.toString() : '0')
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

    // 🚀 Flawless Decimal Validation
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
        SellingPrice: sell, // 🚀 Added to payload
        Discount: disc, // 🚀 Added to payload
        Date: new Date().toISOString()
      }

      // @ts-ignore
      await window.api.receiveStock(payload)

      Swal.fire({
        title: 'Stock Added!',
        text: `Successfully added ${qty} ${quickAddProduct.Unit} to inventory.`,
        icon: 'success',
        timer: 1500,
        showConfirmButton: false
      })

      setQuickAddProduct(null)
      loadData() // Refresh the table
    } catch (err: any) {
      Swal.fire('Error', 'Failed to add stock: ' + err.message, 'error')
    }
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
    return selectedCatId === null
      ? products
      : products.filter((p) => p.CategoryId === selectedCatId)
  }, [products, selectedCatId, searchQuery])

  const getCatName = (id: number | null) => {
    return categories.find((c) => c.Id === id)?.Name || 'N/A'
  }

  return (
    <div className={styles.container}>
      {/* LAYER 1: SMALL FILTER SIDEBAR */}
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

      {/* LAYER 2: DATA TABLE */}
      <div className={styles.panel}>
        <div className={styles.headerRow}>
          <h2 className={styles.panelHeaderTitle}>LIVE CATALOG</h2>
          <input
            type="text"
            className="pos-input"
            placeholder="Search Name or Barcode..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value)
              if (e.target.value) setSelectedCatId(null)
            }}
            style={{ width: '350px' }}
          />
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>CODE</th>
                <th>NAME</th>
                <th>SELL PRICE</th>
                <th>IN STOCK</th>
                <th style={{ textAlign: 'right' }}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {displayedProducts.length === 0 ? (
                <tr>
                  <td
                    colSpan={5}
                    style={{ textAlign: 'center', padding: '50px', color: 'var(--text-muted)' }}
                  >
                    {searchQuery
                      ? 'No products match your search.'
                      : 'No products found in this folder.'}
                  </td>
                </tr>
              ) : (
                displayedProducts.map((product) => (
                  <tr key={product.Id}>
                    <td
                      style={{
                        fontFamily: 'monospace',
                        color: 'var(--text-muted)',
                        fontSize: '13px'
                      }}
                    >
                      {product.Barcode}
                    </td>
                    <td>
                      <div style={{ fontWeight: 800 }}>{product.Name}</div>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                        {getCatName(product.CategoryId)}
                      </div>
                    </td>
                    <td style={{ color: 'var(--action-success)', fontWeight: 900 }}>
                      Rs {(product.SellingPrice || 0).toFixed(2)}
                    </td>
                    <td
                      style={{
                        fontWeight: 900,
                        color:
                          product.Quantity <= 0 ? 'var(--action-danger)' : 'var(--brand-primary)'
                      }}
                    >
                      {product.Quantity || 0}{' '}
                      <span
                        style={{
                          fontSize: '11px',
                          color: 'var(--text-muted)',
                          fontWeight: 'normal'
                        }}
                      >
                        {product.Unit}
                      </span>
                    </td>
                    <td
                      style={{
                        textAlign: 'right',
                        display: 'flex',
                        justifyContent: 'flex-end',
                        gap: '8px'
                      }}
                    >
                      <button
                        className="pos-btn neutral"
                        onClick={() => handleViewProduct(product)}
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                      >
                        INFO
                      </button>
                      <button
                        className="pos-btn success"
                        onClick={() => handleOpenQuickAdd(product)}
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                      >
                        ⚡ QUICK ADD
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* --- 🚀 UPGRADED MODAL: QUICK ADD STOCK --- */}
      {quickAddProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView} style={{ maxWidth: '600px' }}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px', color: 'var(--text-main)' }}>
                ⚡ Fast Stock Receive
              </h2>
              <button
                className="pos-btn neutral"
                style={{ padding: '5px 15px', minHeight: '40px' }}
                onClick={() => setQuickAddProduct(null)}
              >
                Close
              </button>
            </div>

            <form onSubmit={submitQuickAdd} className={styles.modalBody}>
              <div
                style={{
                  marginBottom: '20px',
                  padding: '15px',
                  backgroundColor: 'var(--bg-main)',
                  borderRadius: '8px'
                }}
              >
                <h3 style={{ margin: '0 0 5px 0', color: 'var(--brand-primary)' }}>
                  {quickAddProduct.Name}
                </h3>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 800 }}>
                  CURRENT STOCK:{' '}
                  <span style={{ color: 'var(--text-dark)' }}>
                    {quickAddProduct.Quantity} {quickAddProduct.Unit}
                  </span>
                </div>
              </div>

              {/* Row 1: Qty and Cost */}
              <div style={{ display: 'flex', gap: '15px', marginBottom: '20px' }}>
                <div style={{ flex: 1 }}>
                  <label
                    style={{
                      fontSize: '12px',
                      fontWeight: 800,
                      color: 'var(--text-muted)',
                      textTransform: 'uppercase'
                    }}
                  >
                    Qty Received
                  </label>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <input
                      type="number"
                      step={quickAddProduct.QuantityType === 'kg' ? '0.01' : '1'}
                      className="pos-input"
                      value={quickAddQty}
                      onChange={(e) => setQuickAddQty(e.target.value)}
                      required
                      autoFocus
                    />
                    <span style={{ fontWeight: 800, color: 'var(--text-dark)' }}>
                      {quickAddProduct.Unit}
                    </span>
                  </div>
                </div>

                <div style={{ flex: 1 }}>
                  <label
                    style={{
                      fontSize: '12px',
                      fontWeight: 800,
                      color: 'var(--text-muted)',
                      textTransform: 'uppercase'
                    }}
                  >
                    Supplier Unit Cost
                  </label>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontWeight: 800, color: 'var(--text-muted)' }}>Rs</span>
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

              {/* Row 2: Retail Price and Discount */}
              <div
                style={{
                  display: 'flex',
                  gap: '15px',
                  marginBottom: '20px',
                  padding: '15px',
                  border: '1px solid #cbd5e1',
                  borderRadius: '8px',
                  backgroundColor: '#f8fafc'
                }}
              >
                <div style={{ flex: 1 }}>
                  <label
                    style={{
                      fontSize: '12px',
                      fontWeight: 800,
                      color: 'var(--action-success)',
                      textTransform: 'uppercase'
                    }}
                  >
                    New Selling Price
                  </label>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontWeight: 800, color: 'var(--text-muted)' }}>Rs</span>
                    <input
                      type="number"
                      step="0.01"
                      className="pos-input"
                      style={{ borderColor: 'var(--action-success)' }}
                      value={quickAddSell}
                      onChange={(e) => setQuickAddSell(e.target.value)}
                      required
                    />
                  </div>
                </div>

                <div style={{ flex: 1 }}>
                  <label
                    style={{
                      fontSize: '12px',
                      fontWeight: 800,
                      color: 'var(--action-warning)',
                      textTransform: 'uppercase'
                    }}
                  >
                    Max Allowable Discount
                  </label>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontWeight: 800, color: 'var(--text-muted)' }}>Rs</span>
                    <input
                      type="number"
                      step="0.01"
                      className="pos-input"
                      value={quickAddDiscount}
                      onChange={(e) => setQuickAddDiscount(e.target.value)}
                    />
                  </div>
                </div>
              </div>

              <button type="submit" className="pos-btn success" style={{ width: '100%' }}>
                CONFIRM & ADD BATCH TO INVENTORY
              </button>
            </form>
          </div>
        </div>
      )}

      {/* --- MODAL: BATCH DETAILS --- */}
      {viewingProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '24px', color: 'var(--text-main)' }}>
                  {viewingProduct.Name}
                </h2>
                <div
                  style={{
                    fontSize: '13px',
                    color: 'var(--text-muted)',
                    marginTop: '6px',
                    fontWeight: 600
                  }}
                >
                  Category: {getCatName(viewingProduct.CategoryId)} | Base Unit:{' '}
                  {viewingProduct.Unit} | Code: {viewingProduct.Barcode || 'N/A'}
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
                    color: 'var(--text-muted)',
                    fontWeight: 800,
                    textTransform: 'uppercase'
                  }}
                >
                  Total Stock
                </div>
                <div style={{ fontSize: '28px', fontWeight: 900, color: 'var(--brand-primary)' }}>
                  {viewingProduct.Quantity || 0}{' '}
                  <span style={{ fontSize: '16px', color: 'var(--text-muted)' }}>
                    {viewingProduct.Unit}
                  </span>
                </div>
              </div>
            </div>

            <div className={styles.modalBody}>
              <h3
                style={{
                  fontSize: '15px',
                  marginBottom: '15px',
                  color: 'var(--text-dark)',
                  textTransform: 'uppercase',
                  fontWeight: 800
                }}
              >
                Active Inventory Batches (GRN History)
              </h3>

              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>DATE</th>
                      <th>ORIGINAL QTY</th>
                      <th>CURRENT QTY</th>
                      <th>BUYING</th>
                      <th>SELLING</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productBatches.length === 0 ? (
                      <tr>
                        <td
                          colSpan={5}
                          style={{
                            textAlign: 'center',
                            padding: '50px',
                            color: 'var(--text-muted)',
                            fontWeight: 600
                          }}
                        >
                          No active batches found.
                        </td>
                      </tr>
                    ) : (
                      productBatches.map((batch, idx) => (
                        <tr key={idx}>
                          <td
                            style={{
                              color: 'var(--text-muted)',
                              fontSize: '13px',
                              fontWeight: 600
                            }}
                          >
                            {new Date(batch.ReceivedDate).toLocaleDateString()}
                          </td>
                          <td style={{ color: 'var(--text-muted)' }}>{batch.InitialQuantity}</td>
                          <td
                            style={{ fontWeight: 900, color: 'var(--text-dark)', fontSize: '15px' }}
                          >
                            {batch.RemainingQuantity}
                          </td>
                          <td style={{ color: 'var(--text-muted)' }}>
                            Rs {batch.CostPrice.toFixed(2)}
                          </td>
                          <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                            Rs {batch.SellingPrice.toFixed(2)}
                          </td>
                        </tr>
                      ))
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
    </div>
  )
}
