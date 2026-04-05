// src/renderer/src/pages/POS/POSWorkspace.tsx
import React, { useState, useEffect, useMemo, useRef } from 'react'
import Swal from 'sweetalert2'
import { Product } from '../../types/models'
import styles from './POSWorkspace.module.css'

interface CartItem {
  uid: string
  productId: number
  batchId: number
  name: string
  unitPrice: string
  originalPrice: number
  buyPrice: number
  quantity: string
  maxDiscount: number
  availableStock: number
  unit: string
  qtyType: string // 🚀 NEW: Weight support
}

export default function POSWorkspace() {
  const [products, setProducts] = useState<Product[]>([])
  const [searchQuery, setSearchQuery] = useState('')

  const [cartItems, setCartItems] = useState<CartItem[]>([])
  const [selectedCartUid, setSelectedCartUid] = useState<string | null>(null)

  const [batchModalProduct, setBatchModalProduct] = useState<Product | null>(null)
  const [availableBatches, setAvailableBatches] = useState<any[]>([])

  const [checkoutMode, setCheckoutMode] = useState<'none' | 'cash' | 'credit'>('none')
  const [customerName, setCustomerName] = useState('')

  // 🚀 NEW: Cash Drawer Tracking
  const [cashTendered, setCashTendered] = useState('')
  const [downPayment, setDownPayment] = useState('')

  const [isProcessing, setIsProcessing] = useState(false)
  const searchRef = useRef<HTMLInputElement>(null)

  const loadData = async () => {
    try {
      // @ts-ignore
      setProducts(await window.api.getProducts())
    } catch (err) {
      console.error('Failed to load data', err)
    }
  }

  useEffect(() => {
    loadData()
    searchRef.current?.focus() // Always focus the scanner input on load
  }, [])

  const displayedProducts = useMemo(() => {
    return products.filter((p) => {
      if (p.Quantity <= 0) return false
      const q = searchQuery.toLowerCase()
      return p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))
    })
  }, [products, searchQuery])

  // 🚀 BARCODE SCANNER SUPPORT: If only 1 product matches, auto-add it!
  useEffect(() => {
    if (
      searchQuery &&
      displayedProducts.length === 1 &&
      displayedProducts[0].Barcode === searchQuery
    ) {
      handleProductClick(displayedProducts[0])
      setSearchQuery('') // Clear search instantly so next scan works
    }
  }, [searchQuery, displayedProducts])

  const handleProductClick = async (product: Product) => {
    try {
      // @ts-ignore
      const batches = await window.api.getProductBatches(product.Id)
      const activeBatches = batches.filter((b: any) => b.RemainingQuantity > 0)

      if (activeBatches.length === 0) {
        return Swal.fire('Out of Stock', 'No active batches found for this product.', 'error')
      }

      if (activeBatches.length === 1) {
        addToCart(product, activeBatches[0])
      } else {
        setAvailableBatches(activeBatches)
        setBatchModalProduct(product)
      }
    } catch (err) {
      Swal.fire('Error', 'Error checking stock batches.', 'error')
    }
  }

  const addToCart = (product: Product, batch: any) => {
    const existingIndex = cartItems.findIndex(
      (i) => i.productId === product.Id && i.batchId === batch.Id
    )

    if (existingIndex >= 0) {
      const existingItem = cartItems[existingIndex]
      const currentQty = parseFloat(existingItem.quantity) || 0

      if (currentQty >= existingItem.availableStock) {
        return Swal.fire(
          'Stock Limit',
          `Only ${existingItem.availableStock} left in this batch!`,
          'warning'
        )
      }

      const updatedCart = [...cartItems]
      updatedCart[existingIndex].quantity = (currentQty + 1).toString()
      setCartItems(updatedCart)
      setSelectedCartUid(existingItem.uid)
      setBatchModalProduct(null)
      return
    }

    const newUid = Math.random().toString(36).substr(2, 9)
    const newItem: CartItem = {
      uid: newUid,
      productId: product.Id,
      batchId: batch.Id,
      name: product.PrintName || product.Name, // 🚀 NEW: Use PrintName for receipt layout!
      unitPrice: batch.SellingPrice.toString(),
      originalPrice: batch.SellingPrice,
      buyPrice: batch.CostPrice,
      quantity: '1',
      maxDiscount: batch.Discount,
      availableStock: batch.RemainingQuantity,
      unit: product.Unit || 'Pcs',
      qtyType: product.QuantityType || 'quantity'
    }
    setCartItems([newItem, ...cartItems])
    setSelectedCartUid(newUid)
    setBatchModalProduct(null)
    searchRef.current?.focus()
  }

  const handleUpdateCart = (uid: string, field: 'quantity' | 'unitPrice', value: string) => {
    if (value !== '' && !/^\d*\.?\d*$/.test(value)) return

    setCartItems((prev) =>
      prev.map((item) => {
        if (item.uid !== uid) return item

        if (field === 'quantity') {
          const numVal = parseFloat(value) || 0
          if (numVal > item.availableStock) {
            Swal.fire('Stock Limit', `Only ${item.availableStock} left!`, 'warning')
            return item
          }
          // 🚀 Decimal Validation
          if (item.qtyType === 'quantity' && numVal % 1 !== 0) {
            Swal.fire('Invalid Qty', `Cannot sell partial amounts of ${item.name}`, 'error')
            return item
          }
        }
        return { ...item, [field]: value }
      })
    )
  }

  const handleRemoveCartItem = (uid: string) => {
    setCartItems((prev) => prev.filter((item) => item.uid !== uid))
    if (selectedCartUid === uid) setSelectedCartUid(null)
  }

  const activeEditItem = cartItems.find((i) => i.uid === selectedCartUid)
  const activeUnitPrice = activeEditItem ? parseFloat(activeEditItem.unitPrice) || 0 : 0
  const activeQty = activeEditItem ? parseFloat(activeEditItem.quantity) || 0 : 0

  const subTotal = cartItems.reduce(
    (sum, item) => sum + (parseFloat(item.quantity) || 0) * item.originalPrice,
    0
  )
  const totalDiscount = cartItems.reduce((sum, item) => {
    const q = parseFloat(item.quantity) || 0
    const p = parseFloat(item.unitPrice) || 0
    const discountPerItem = item.originalPrice - p
    return sum + (discountPerItem > 0 ? discountPerItem * q : 0)
  }, 0)
  const grandTotal = subTotal - totalDiscount

  // SECURITY CHECKS
  const hasLossItem = cartItems.some((item) => (parseFloat(item.unitPrice) || 0) < item.buyPrice)
  const hasZeroQty = cartItems.some((item) => (parseFloat(item.quantity) || 0) <= 0)

  // 🚀 NEW: EXACT CHANGE MATH
  const tenderedNum = parseFloat(cashTendered) || 0
  const changeToGive = tenderedNum > grandTotal ? tenderedNum - grandTotal : 0
  const balanceDue = Math.max(0, grandTotal - (parseFloat(downPayment) || 0))

  const handleProcessSale = async (e?: React.FormEvent) => {
    if (e) e.preventDefault()

    if (cartItems.length === 0) return Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
    if (hasLossItem) return Swal.fire('Sale Blocked', 'Items priced below cost!', 'error')
    if (hasZeroQty) return Swal.fire('Sale Blocked', 'Items have a quantity of 0!', 'error')

    if (checkoutMode === 'credit' && !customerName.trim()) {
      return Swal.fire('Required Field', 'Customer name is required for a credit sale!', 'warning')
    }

    if (checkoutMode === 'cash' && tenderedNum < grandTotal) {
      return Swal.fire('Insufficient Cash', 'Cash tendered is less than the total amount.', 'error')
    }

    setIsProcessing(true)
    try {
      const receiptId = `INV-${Date.now()}`
      const isCredit = checkoutMode === 'credit'
      const paidAmt = isCredit ? parseFloat(downPayment) || 0 : grandTotal

      let status = 0
      if (isCredit) {
        if (paidAmt === 0) status = 1
        else if (paidAmt < grandTotal) status = 2
      }

      // 🚀 NEW: Sending Exact Cash math to Database!
      const transaction = {
        ReceiptId: receiptId,
        TransactionDate: new Date().toISOString(),
        TotalAmount: grandTotal,
        PaidAmount: paidAmt,
        CashReceived: isCredit ? paidAmt : tenderedNum,
        ChangeGiven: isCredit ? 0 : changeToGive,
        IsCredit: isCredit ? 1 : 0,
        CustomerName: isCredit ? customerName : 'Walk-in Customer',
        Status: status
      }

      const movements = cartItems.map((item) => ({
        ProductId: item.productId,
        Quantity: parseFloat(item.quantity) || 0,
        UnitCost: item.originalPrice,
        UnitPrice: parseFloat(item.unitPrice) || 0,
        StockBatchId: item.batchId,
        Note: ''
      }))

      // @ts-ignore
      await window.api.processCompleteSale(transaction, movements)

      setCartItems([])
      setSelectedCartUid(null)
      setCheckoutMode('none')
      setCustomerName('')
      setDownPayment('')
      setCashTendered('')
      loadData()

      Swal.fire({
        title: '✅ Sale Successful!',
        text: `Change to give: Rs ${changeToGive.toFixed(2)}`,
        icon: 'success'
      })
      searchRef.current?.focus()
    } catch (error: any) {
      Swal.fire('Checkout Failed', error.message, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleFastCheckout = async () => {
    if (cartItems.length === 0) return Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
    if (hasLossItem) return Swal.fire('Sale Blocked', 'Items priced below cost!', 'error')
    if (hasZeroQty) return Swal.fire('Sale Blocked', 'Items have 0 quantity!', 'error')

    setIsProcessing(true)
    try {
      const receiptId = `INV-${Date.now()}`
      const transaction = {
        ReceiptId: receiptId,
        TransactionDate: new Date().toISOString(),
        TotalAmount: grandTotal,
        PaidAmount: grandTotal,
        CashReceived: grandTotal, // Exact amount assumed for fast checkout
        ChangeGiven: 0,
        IsCredit: 0,
        CustomerName: 'Walk-in Customer',
        Status: 0
      }

      const movements = cartItems.map((item) => ({
        ProductId: item.productId,
        Quantity: parseFloat(item.quantity) || 0,
        UnitCost: item.originalPrice,
        UnitPrice: parseFloat(item.unitPrice) || 0,
        StockBatchId: item.batchId,
        Note: ''
      }))

      // @ts-ignore
      await window.api.processCompleteSale(transaction, movements)

      setCartItems([])
      setSelectedCartUid(null)
      loadData()

      Swal.fire({
        title: '✅ Fast Checkout Complete!',
        text: `Receipt ID: ${receiptId}`,
        icon: 'success',
        timer: 1500,
        showConfirmButton: false
      })
      searchRef.current?.focus()
    } catch (error: any) {
      Swal.fire('Checkout Failed', error.message, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  return (
    <div className={styles.container}>
      <div className={styles.leftColumn}>
        {/* TOP LEFT: PRODUCT CATALOG GRID */}
        <div className={styles.gridPanel}>
          <div className={styles.gridHeader}>
            <span style={{ fontSize: '20px', fontWeight: 900 }}>PRODUCTS CATALOG</span>
            <input
              ref={searchRef}
              type="text"
              className="pos-input"
              placeholder="Search or Scan Barcode..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{ width: '300px' }}
            />
          </div>

          <div className={styles.productsArea}>
            {displayedProducts.map((p) => (
              <button
                key={p.Id}
                className={styles.productCardBtn}
                onClick={() => handleProductClick(p)}
              >
                <div className={styles.productName} title={p.Name}>
                  {p.PrintName || p.Name}
                </div>
                <div className={styles.productPriceRow}>
                  <span className={styles.productPrice}>Rs {(p.SellingPrice || 0).toFixed(2)}</span>
                  <span
                    className={`${styles.productStock} ${p.Quantity <= 0 ? styles.outOfStock : ''}`}
                  >
                    {p.Quantity} {p.Unit}
                  </span>
                </div>
              </button>
            ))}
            {displayedProducts.length === 0 && (
              <div
                style={{
                  gridColumn: '1 / -1',
                  textAlign: 'center',
                  padding: '40px',
                  color: 'var(--text-muted)'
                }}
              >
                No items found.
              </div>
            )}
          </div>
        </div>

        {/* BOTTOM LEFT: PROFESSIONAL EDIT PANEL */}
        {activeEditItem ? (
          <div className={styles.editPanelActive}>
            <div className={styles.editHeader}>
              <div className={styles.editName}>
                {activeEditItem.name}{' '}
                <span style={{ fontSize: '15px', color: 'var(--text-muted)' }}>
                  | Stock: {activeEditItem.availableStock}
                </span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
                <div className={styles.editTotal}>
                  Rs {(activeQty * activeUnitPrice).toFixed(2)}
                </div>
                <button
                  className="pos-btn neutral"
                  onClick={() => setSelectedCartUid(null)}
                  style={{ padding: '10px 15px' }}
                >
                  ✖
                </button>
              </div>
            </div>

            <div className={styles.editControls}>
              <div className={styles.qtyWrapper}>
                <label className={styles.editLabel}>Quantity ({activeEditItem.unit})</label>
                <div className={styles.qtyStepper}>
                  <button
                    className="pos-btn neutral"
                    style={{ fontSize: '24px', padding: '10px 20px' }}
                    onClick={() =>
                      handleUpdateCart(
                        activeEditItem.uid,
                        'quantity',
                        Math.max(1, activeQty - 1).toString()
                      )
                    }
                  >
                    −
                  </button>
                  <input
                    type="number"
                    step={activeEditItem.qtyType === 'kg' ? '0.01' : '1'}
                    className="pos-input"
                    value={activeEditItem.quantity}
                    onChange={(e) =>
                      handleUpdateCart(activeEditItem.uid, 'quantity', e.target.value)
                    }
                    style={{ textAlign: 'center', fontSize: '24px', width: '120px' }}
                  />
                  <button
                    className="pos-btn neutral"
                    style={{ fontSize: '24px', padding: '10px 20px' }}
                    onClick={() =>
                      handleUpdateCart(activeEditItem.uid, 'quantity', (activeQty + 1).toString())
                    }
                  >
                    +
                  </button>
                </div>
              </div>

              <div className={styles.editInfoBox}>
                <div className={styles.infoRow}>
                  Original Price:{' '}
                  <span style={{ fontWeight: 900 }}>
                    Rs {activeEditItem.originalPrice.toFixed(2)}
                  </span>
                </div>
                <div className={styles.infoRow}>
                  Max Discount Allowable:{' '}
                  <span style={{ fontWeight: 900, color: 'var(--action-warning)' }}>
                    Rs {activeEditItem.maxDiscount.toFixed(2)}
                  </span>
                </div>

                {activeEditItem.maxDiscount > 0 &&
                  activeUnitPrice > activeEditItem.originalPrice - activeEditItem.maxDiscount && (
                    <button
                      className="pos-btn warning"
                      onClick={() =>
                        handleUpdateCart(
                          activeEditItem.uid,
                          'unitPrice',
                          (activeEditItem.originalPrice - activeEditItem.maxDiscount).toString()
                        )
                      }
                      style={{ width: '100%', marginTop: '10px' }}
                    >
                      ⚡ Apply Max Discount
                    </button>
                  )}
              </div>

              <div className={styles.priceWrapper}>
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-end',
                    width: '100%',
                    marginBottom: '8px'
                  }}
                >
                  <label className={styles.editLabel} style={{ marginBottom: 0 }}>
                    Unit Price
                  </label>
                  {activeUnitPrice < activeEditItem.buyPrice ? (
                    <span
                      style={{ color: 'var(--action-danger)', fontWeight: 900, fontSize: '12px' }}
                    >
                      ⛔ BELOW COST (Rs {activeEditItem.buyPrice.toFixed(2)})!
                    </span>
                  ) : activeUnitPrice <
                    activeEditItem.originalPrice - activeEditItem.maxDiscount ? (
                    <span
                      style={{ color: 'var(--action-warning)', fontWeight: 900, fontSize: '12px' }}
                    >
                      ⚠️ OVER MAX DISCOUNT
                    </span>
                  ) : null}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <span style={{ fontSize: '20px', fontWeight: 900, color: 'var(--text-muted)' }}>
                    Rs
                  </span>
                  <input
                    type="number"
                    step="0.01"
                    className="pos-input"
                    value={activeEditItem.unitPrice}
                    onChange={(e) =>
                      handleUpdateCart(activeEditItem.uid, 'unitPrice', e.target.value)
                    }
                    style={{ fontSize: '24px' }}
                  />
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div
            className={styles.editPanelActive}
            style={{
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--text-muted)',
              border: '2px dashed #cbd5e1',
              boxShadow: 'none'
            }}
          >
            <h3 style={{ margin: 0, fontSize: '22px' }}>Current Order</h3>
            <p style={{ margin: '5px 0 0 0', fontSize: '15px' }}>
              Tap an item in the receipt to apply discounts or change quantities.
            </p>
          </div>
        )}
      </div>

      {/* ========================================= */}
      {/* RIGHT COLUMN (Receipt & Total)            */}
      {/* ========================================= */}
      <div className={styles.rightColumn}>
        <div className={styles.cartHeader}>
          <span style={{ fontSize: '18px', fontWeight: 900 }}>
            Receipt{' '}
            <span style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
              ({cartItems.length})
            </span>
          </span>
          <button
            className="pos-btn neutral"
            onClick={() => {
              setCartItems([])
              setSelectedCartUid(null)
              searchRef.current?.focus()
            }}
            style={{ padding: '10px 15px', minHeight: '40px', fontSize: '12px' }}
          >
            CLEAR
          </button>
        </div>

        <div className={styles.cartItemsArea}>
          {cartItems.length === 0 ? (
            <div
              style={{
                textAlign: 'center',
                padding: '40px',
                color: 'var(--text-muted)',
                fontWeight: 600
              }}
            >
              Scan an item to begin.
            </div>
          ) : (
            cartItems.map((item) => {
              const itemPrice = parseFloat(item.unitPrice) || 0
              const itemQty = parseFloat(item.quantity) || 0
              const isBelowCost = itemPrice < item.buyPrice
              const isOverDiscount =
                itemPrice < item.originalPrice - item.maxDiscount && !isBelowCost

              return (
                <button
                  key={item.uid}
                  className={`${styles.cartItemBtn} ${selectedCartUid === item.uid ? styles.active : ''} ${isBelowCost ? styles.cartItemDanger : ''}`}
                  onClick={() => setSelectedCartUid(item.uid)}
                >
                  <div className={styles.cartItemLeft}>
                    <div className={styles.cartItemName}>{item.name}</div>
                    <div className={styles.cartItemDetails}>
                      Rs {itemPrice.toFixed(2)} × {itemQty} {item.unit}
                    </div>
                    {itemPrice < item.originalPrice && (
                      <div
                        style={{
                          fontSize: '12px',
                          fontWeight: 800,
                          marginTop: '4px',
                          color: isBelowCost
                            ? 'var(--action-danger)'
                            : isOverDiscount
                              ? 'var(--action-warning)'
                              : 'var(--brand-primary)'
                        }}
                      >
                        {isBelowCost
                          ? '⛔ LOSS'
                          : isOverDiscount
                            ? '⚠️ OVER DISC'
                            : `Original: Rs ${item.originalPrice.toFixed(2)}`}
                      </div>
                    )}
                  </div>
                  <div className={styles.cartItemRight}>
                    <div className={styles.cartItemPrice}>
                      Rs {(itemQty * itemPrice).toFixed(2)}
                    </div>
                    <div
                      className="pos-btn danger"
                      style={{ minHeight: '35px', padding: '5px 10px' }}
                      onClick={(e) => {
                        e.stopPropagation()
                        handleRemoveCartItem(item.uid)
                      }}
                    >
                      ✖
                    </div>
                  </div>
                </button>
              )
            })
          )}
        </div>

        <div className={styles.checkoutFooter}>
          {hasLossItem && (
            <div
              style={{
                backgroundColor: '#fef2f2',
                color: '#dc2626',
                padding: '10px',
                borderRadius: '6px',
                fontSize: '12px',
                fontWeight: 800,
                textAlign: 'center',
                marginBottom: '15px'
              }}
            >
              ⛔ SALE BLOCKED: Items priced below cost!
            </div>
          )}
          {hasZeroQty && (
            <div
              style={{
                backgroundColor: '#fffbeb',
                color: '#d97706',
                padding: '10px',
                borderRadius: '6px',
                fontSize: '12px',
                fontWeight: 800,
                textAlign: 'center',
                marginBottom: '15px'
              }}
            >
              ⚠️ SALE BLOCKED: Quantity cannot be 0.
            </div>
          )}

          <div className={styles.summaryRow}>
            <span>Subtotal</span>
            <span>Rs {subTotal.toFixed(2)}</span>
          </div>
          <div className={`${styles.summaryRow} ${totalDiscount > 0 ? styles.discount : ''}`}>
            <span>Discount</span>
            <span>
              {totalDiscount > 0 ? '-' : ''} Rs {totalDiscount.toFixed(2)}
            </span>
          </div>
          <div className={styles.summaryTotalRow}>
            <span style={{ fontSize: '16px', fontWeight: 900 }}>GRAND TOTAL</span>
            <span style={{ fontSize: '32px', fontWeight: 900, color: 'var(--brand-primary)' }}>
              Rs {grandTotal.toFixed(2)}
            </span>
          </div>

          <div className={styles.checkoutBtnGrid}>
            <button
              className="pos-btn success"
              onClick={() => {
                if (cartItems.length > 0) setCheckoutMode('cash')
                else Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
              }}
              disabled={hasLossItem || hasZeroQty}
            >
              PAY (EXACT CHANGE)
            </button>
            <button
              className="pos-btn warning"
              onClick={() => {
                if (cartItems.length > 0) setCheckoutMode('credit')
                else Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
              }}
              disabled={hasLossItem || hasZeroQty}
            >
              CREDIT SALE
            </button>
            <button
              className="pos-btn neutral"
              onClick={handleFastCheckout}
              disabled={isProcessing || hasLossItem || hasZeroQty}
            >
              {isProcessing ? '...' : 'FAST CHECKOUT'}
            </button>
          </div>
        </div>
      </div>

      {/* ========================================= */}
      {/* BATCH SELECTION MODAL                     */}
      {/* ========================================= */}
      {batchModalProduct && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px' }}>Select Batch</h2>
              <button className="pos-btn neutral" onClick={() => setBatchModalProduct(null)}>
                ✖
              </button>
            </div>
            <div className={styles.modalBody}>
              <p>
                Multiple batches found for <b>{batchModalProduct.Name}</b>. Which one to sell?
              </p>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                {availableBatches.map((batch) => (
                  <button
                    key={batch.Id}
                    className="pos-btn neutral"
                    style={{ justifyContent: 'space-between', padding: '15px' }}
                    onClick={() => addToCart(batchModalProduct, batch)}
                  >
                    <div style={{ textAlign: 'left' }}>
                      <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-dark)' }}>
                        {batch.SupplierName || 'Stock Entry'}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        Stock: {batch.RemainingQuantity} | Rec:{' '}
                        {new Date(batch.ReceivedDate).toLocaleDateString()}
                      </div>
                    </div>
                    <div
                      style={{ fontSize: '20px', fontWeight: 900, color: 'var(--brand-primary)' }}
                    >
                      Rs {batch.SellingPrice.toFixed(2)}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ========================================= */}
      {/* 🚀 NEW: CHECKOUT MODALS (CASH & CREDIT)   */}
      {/* ========================================= */}
      {checkoutMode !== 'none' && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox} style={{ maxWidth: '600px' }}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '24px' }}>
                {checkoutMode === 'cash' ? 'Cash Checkout' : 'Credit Sale'}
              </h2>
            </div>

            <form onSubmit={handleProcessSale} className={styles.modalBody}>
              <div
                style={{
                  background: 'var(--bg-main)',
                  padding: '20px',
                  borderRadius: '8px',
                  textAlign: 'center',
                  marginBottom: '20px'
                }}
              >
                <div
                  style={{
                    fontSize: '16px',
                    fontWeight: 800,
                    color: 'var(--text-muted)',
                    textTransform: 'uppercase'
                  }}
                >
                  Total Amount Due
                </div>
                <div style={{ fontSize: '48px', fontWeight: 900, color: 'var(--text-dark)' }}>
                  Rs {grandTotal.toFixed(2)}
                </div>
              </div>

              {checkoutMode === 'cash' && (
                <>
                  <div style={{ marginBottom: '20px' }}>
                    <label
                      style={{
                        fontSize: '14px',
                        fontWeight: 800,
                        color: 'var(--text-muted)',
                        textTransform: 'uppercase'
                      }}
                    >
                      Cash Handed by Customer
                    </label>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '15px',
                        marginTop: '10px'
                      }}
                    >
                      <span
                        style={{ fontSize: '28px', fontWeight: 900, color: 'var(--text-muted)' }}
                      >
                        Rs
                      </span>
                      <input
                        type="number"
                        step="0.01"
                        className="pos-input"
                        style={{ fontSize: '32px', height: '70px', fontWeight: 900 }}
                        value={cashTendered}
                        onChange={(e) => setCashTendered(e.target.value)}
                        required
                        autoFocus
                        placeholder="0.00"
                      />
                    </div>
                  </div>

                  {tenderedNum > 0 && tenderedNum >= grandTotal && (
                    <div
                      style={{
                        background: '#f0fdf4',
                        border: '2px solid #16a34a',
                        padding: '20px',
                        borderRadius: '8px',
                        textAlign: 'center',
                        marginBottom: '20px'
                      }}
                    >
                      <div
                        style={{
                          fontSize: '16px',
                          fontWeight: 800,
                          color: '#15803d',
                          textTransform: 'uppercase'
                        }}
                      >
                        Change to Return
                      </div>
                      <div style={{ fontSize: '42px', fontWeight: 900, color: '#16a34a' }}>
                        Rs {changeToGive.toFixed(2)}
                      </div>
                    </div>
                  )}
                </>
              )}

              {checkoutMode === 'credit' && (
                <>
                  <div style={{ marginBottom: '20px' }}>
                    <label
                      style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      Customer Name *
                    </label>
                    <input
                      type="text"
                      className="pos-input"
                      style={{ height: '60px', fontSize: '20px' }}
                      value={customerName}
                      onChange={(e) => setCustomerName(e.target.value)}
                      required
                      autoFocus
                      placeholder="John Doe"
                    />
                  </div>
                  <div style={{ marginBottom: '20px' }}>
                    <label
                      style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      Initial Payment (Optional)
                    </label>
                    <input
                      type="number"
                      step="0.01"
                      className="pos-input"
                      style={{ height: '60px', fontSize: '20px' }}
                      value={downPayment}
                      onChange={(e) => setDownPayment(e.target.value)}
                      placeholder="0.00"
                    />
                  </div>
                  <div
                    style={{
                      background: '#fffbeb',
                      border: '2px solid #d97706',
                      padding: '20px',
                      borderRadius: '8px',
                      textAlign: 'center',
                      marginBottom: '20px'
                    }}
                  >
                    <div
                      style={{
                        fontSize: '16px',
                        fontWeight: 800,
                        color: '#b45309',
                        textTransform: 'uppercase'
                      }}
                    >
                      Remaining Debt
                    </div>
                    <div style={{ fontSize: '42px', fontWeight: 900, color: '#d97706' }}>
                      Rs {balanceDue.toFixed(2)}
                    </div>
                  </div>
                </>
              )}

              <div style={{ display: 'flex', gap: '15px' }}>
                <button
                  type="button"
                  className="pos-btn neutral"
                  style={{ flex: 1 }}
                  onClick={() => setCheckoutMode('none')}
                  disabled={isProcessing}
                >
                  CANCEL
                </button>
                <button
                  type="submit"
                  className="pos-btn success"
                  style={{ flex: 2 }}
                  disabled={isProcessing || (checkoutMode === 'cash' && tenderedNum < grandTotal)}
                >
                  {isProcessing ? 'Processing...' : 'COMPLETE SALE'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
