// src/renderer/src/pages/POS/POSWorkspace.tsx
import React, { useState, useEffect, useMemo, useRef } from 'react'
import Swal from 'sweetalert2'
import { Product } from '../../types/models'
import { RiBankCardLine, RiMoneyDollarBoxLine, RiFlashlightFill } from 'react-icons/ri'
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
  qtyType: string
}

export default function POSWorkspace() {
  const [products, setProducts] = useState<Product[]>([])
  const [searchQuery, setSearchQuery] = useState('')

  const [cartItems, setCartItems] = useState<CartItem[]>([])
  const [selectedCartUid, setSelectedCartUid] = useState<string | null>(null)

  const [batchModalProduct, setBatchModalProduct] = useState<Product | null>(null)
  const [availableBatches, setAvailableBatches] = useState<any[]>([])

  const [checkoutMode, setCheckoutMode] = useState<'none' | 'cash' | 'card'>('none')
  const [cashTendered, setCashTendered] = useState('')

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
    searchRef.current?.focus()
  }, [])

  useEffect(() => {
    const handleGlobalKeyDown = (e: KeyboardEvent) => {
      if (
        checkoutMode === 'none' &&
        !batchModalProduct &&
        document.activeElement?.tagName !== 'INPUT' &&
        e.key.length === 1
      ) {
        searchRef.current?.focus()
      }
    }
    window.addEventListener('keydown', handleGlobalKeyDown)
    return () => window.removeEventListener('keydown', handleGlobalKeyDown)
  }, [checkoutMode, batchModalProduct])

  const displayedProducts = useMemo(() => {
    return products.filter((p) => {
      if (p.Quantity <= 0) return false
      const q = searchQuery.toLowerCase()
      return p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))
    })
  }, [products, searchQuery])

  useEffect(() => {
    if (
      searchQuery &&
      displayedProducts.length === 1 &&
      displayedProducts[0].Barcode === searchQuery
    ) {
      handleProductClick(displayedProducts[0])
      setSearchQuery('')
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
      name: product.PrintName || product.Name,
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

  const hasLossItem = cartItems.some((item) => (parseFloat(item.unitPrice) || 0) < item.buyPrice)
  const hasZeroQty = cartItems.some((item) => (parseFloat(item.quantity) || 0) <= 0)

  const tenderedNum = parseFloat(cashTendered) || 0
  const changeToGive = tenderedNum > grandTotal ? tenderedNum - grandTotal : 0

  const handleProcessSale = async (e?: React.FormEvent) => {
    if (e) e.preventDefault()

    if (cartItems.length === 0) return Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
    if (hasLossItem) return Swal.fire('Sale Blocked', 'Items priced below cost!', 'error')
    if (hasZeroQty) return Swal.fire('Sale Blocked', 'Items have a quantity of 0!', 'error')

    if (checkoutMode === 'cash' && tenderedNum < grandTotal) {
      return Swal.fire('Insufficient Cash', 'Cash tendered is less than the total amount.', 'error')
    }

    setIsProcessing(true)
    try {
      const receiptId = `INV-${Date.now()}`

      const transaction = {
        ReceiptId: receiptId,
        TransactionDate: new Date().toISOString(),
        TotalAmount: grandTotal,
        PaidAmount: grandTotal,
        CashReceived: checkoutMode === 'cash' ? tenderedNum : grandTotal,
        ChangeGiven: checkoutMode === 'cash' ? changeToGive : 0,
        IsCredit: 0,
        PaymentMethod: checkoutMode.toUpperCase(),
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
      setCheckoutMode('none')
      setCashTendered('')
      loadData()

      if (checkoutMode === 'cash' && changeToGive > 0) {
        Swal.fire({
          title: '✅ Sale Successful!',
          text: `Change to give: Rs ${changeToGive.toFixed(2)}`,
          icon: 'success'
        })
      } else {
        Swal.fire({
          title: '✅ Sale Successful!',
          icon: 'success',
          timer: 1500,
          showConfirmButton: false
        })
      }

      searchRef.current?.focus()
    } catch (error: any) {
      Swal.fire('Checkout Failed', error.message, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleFastCash = () => {
    setCashTendered(grandTotal.toString())
    setCheckoutMode('cash')
  }

  return (
    <div className={styles.container}>
      <div className={styles.leftColumn}>
        {/* TOP LEFT: PRODUCT CATALOG GRID */}
        <div className={styles.gridPanel}>
          <div className={styles.gridHeader}>
            <span className={styles.gridTitle}>PRODUCTS CATALOG</span>
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
              <div className={styles.emptyGridState}>No items found.</div>
            )}
          </div>
        </div>

        {/* BOTTOM LEFT: PROFESSIONAL EDIT PANEL */}
        {activeEditItem ? (
          <div className={styles.editPanelActive}>
            <div className={styles.editHeader}>
              <div className={styles.editNameWrapper}>
                <span className={styles.editName} title={activeEditItem.name}>
                  {activeEditItem.name}
                </span>
                <span className={styles.editStockMeta}>
                  | Stock: {activeEditItem.availableStock}
                </span>
              </div>
              <div className={styles.editHeaderRight}>
                <div className={styles.editTotal}>
                  Rs {(activeQty * activeUnitPrice).toFixed(2)}
                </div>
                <button
                  className={`pos-btn neutral ${styles.closeEditBtn}`}
                  onClick={() => setSelectedCartUid(null)}
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
                    className={`pos-btn neutral ${styles.stepperBtn}`}
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
                    className={`pos-input ${styles.stepperInput}`}
                    value={activeEditItem.quantity}
                    onChange={(e) =>
                      handleUpdateCart(activeEditItem.uid, 'quantity', e.target.value)
                    }
                  />
                  <button
                    className={`pos-btn neutral ${styles.stepperBtn}`}
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
                  <span className={styles.infoValue}>
                    Rs {activeEditItem.originalPrice.toFixed(2)}
                  </span>
                </div>
                <div className={styles.infoRow}>
                  Max Discount:{' '}
                  <span className={styles.infoValueWarning}>
                    Rs {activeEditItem.maxDiscount.toFixed(2)}
                  </span>
                </div>
              </div>

              <div className={styles.priceWrapper}>
                <div className={styles.priceHeader}>
                  <label className={styles.editLabel}>Unit Price</label>
                  {activeUnitPrice < activeEditItem.buyPrice ? (
                    <span className={styles.priceWarningDanger}>
                      ⛔ BELOW COST (Rs {activeEditItem.buyPrice.toFixed(2)})!
                    </span>
                  ) : activeUnitPrice <
                    activeEditItem.originalPrice - activeEditItem.maxDiscount ? (
                    <span className={styles.priceWarningOver}>⚠️ OVER MAX DISCOUNT</span>
                  ) : null}
                </div>

                {/* 🚀 DISCOUNT BTN MOVED HERE */}
                <div className={styles.priceInputWrapper}>
                  <span className={styles.priceCurrency}>Rs</span>
                  <input
                    type="number"
                    step="0.01"
                    className={`pos-input ${styles.priceInputHuge}`}
                    value={activeEditItem.unitPrice}
                    onChange={(e) =>
                      handleUpdateCart(activeEditItem.uid, 'unitPrice', e.target.value)
                    }
                  />
                  {activeEditItem.maxDiscount > 0 &&
                    activeUnitPrice > activeEditItem.originalPrice - activeEditItem.maxDiscount && (
                      <button
                        className={`pos-btn-sm warning ${styles.inlineDiscBtn}`}
                        onClick={() =>
                          handleUpdateCart(
                            activeEditItem.uid,
                            'unitPrice',
                            (activeEditItem.originalPrice - activeEditItem.maxDiscount).toString()
                          )
                        }
                        title={`Apply Max Discount (Rs ${activeEditItem.maxDiscount})`}
                      >
                        ⚡ MAX DISC
                      </button>
                    )}
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div className={styles.editPanelInactive}>
            <h3 className={styles.inactiveTitle}>Current Order</h3>
            <p className={styles.inactiveSubtitle}>
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
          <span className={styles.cartTitle}>
            Receipt <span className={styles.cartCount}>({cartItems.length})</span>
          </span>
          <button
            className={`pos-btn neutral ${styles.clearBtn}`}
            onClick={() => {
              setCartItems([])
              setSelectedCartUid(null)
              searchRef.current?.focus()
            }}
          >
            CLEAR
          </button>
        </div>

        <div className={styles.cartItemsArea}>
          {cartItems.length === 0 ? (
            <div className={styles.emptyCartState}>Scan an item to begin.</div>
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
                        className={`${styles.cartItemDiscountMeta} ${isBelowCost ? styles.textDanger : isOverDiscount ? styles.textWarning : styles.textSuccess}`}
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
                      className={`pos-btn danger ${styles.removeCartBtn}`}
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
            <div className={`${styles.blockerAlert} ${styles.dangerAlert}`}>
              ⛔ SALE BLOCKED: Items priced below cost!
            </div>
          )}
          {hasZeroQty && (
            <div className={`${styles.blockerAlert} ${styles.warningAlert}`}>
              ⚠️ SALE BLOCKED: Quantity cannot be 0.
            </div>
          )}

          <div className={styles.summaryRow}>
            <span>Subtotal</span>
            <span>Rs {subTotal.toFixed(2)}</span>
          </div>
          <div className={`${styles.summaryRow} ${totalDiscount > 0 ? styles.discountText : ''}`}>
            <span>Discount</span>
            <span>
              {totalDiscount > 0 ? '-' : ''} Rs {totalDiscount.toFixed(2)}
            </span>
          </div>
          <div className={styles.summaryTotalRow}>
            <span className={styles.grandTotalLabel}>GRAND TOTAL</span>
            <span className={styles.grandTotalValue}>Rs {grandTotal.toFixed(2)}</span>
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
              <RiMoneyDollarBoxLine size={24} /> CASH
            </button>
            <button
              className="pos-btn warning"
              onClick={() => {
                if (cartItems.length > 0) setCheckoutMode('card')
                else Swal.fire('Empty Cart', 'Cart is empty!', 'warning')
              }}
              disabled={hasLossItem || hasZeroQty}
            >
              <RiBankCardLine size={24} /> CARD
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
              <h2 className="pos-page-title">Select Batch</h2>
              <button className="pos-btn neutral" onClick={() => setBatchModalProduct(null)}>
                ✖
              </button>
            </div>
            <div className={styles.modalBody}>
              <p className={styles.modalSubtitle}>
                Multiple batches found for <b>{batchModalProduct.Name}</b>. Which one to sell?
              </p>
              <div className={styles.batchList}>
                {availableBatches.map((batch) => (
                  <button
                    key={batch.Id}
                    className={`pos-btn neutral ${styles.batchBtn}`}
                    onClick={() => addToCart(batchModalProduct, batch)}
                  >
                    <div className={styles.batchBtnLeft}>
                      <div className={styles.batchSupplier}>
                        {batch.SupplierName || 'Stock Entry'}
                      </div>
                      <div className={styles.batchMeta}>
                        Stock: {batch.RemainingQuantity} | Rec:{' '}
                        {new Date(batch.ReceivedDate).toLocaleDateString()}
                      </div>
                    </div>
                    <div className={styles.batchBtnRight}>Rs {batch.SellingPrice.toFixed(2)}</div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ========================================= */}
      {/* 🚀 CHECKOUT MODALS (CASH & CARD)          */}
      {/* ========================================= */}
      {checkoutMode !== 'none' && (
        <div className={styles.modalOverlay}>
          <div className={`${styles.modalBox} ${styles.checkoutModal}`}>
            <div className={styles.modalHeader}>
              <h2 className="pos-page-title">
                {checkoutMode === 'cash' ? 'Cash Checkout' : 'Card Checkout'}
              </h2>
            </div>

            <form onSubmit={handleProcessSale} className={styles.modalBody}>
              <div className={styles.totalDueBox}>
                <div className={styles.totalDueLabel}>Total Amount Due</div>
                <div className={styles.totalDueValue}>Rs {grandTotal.toFixed(2)}</div>
              </div>

              {/* 💵 CASH WORKFLOW */}
              {checkoutMode === 'cash' && (
                <>
                  <div className={styles.cashInputSection}>
                    <label className={styles.inputLabel}>Cash Handed by Customer</label>
                    <div className={styles.cashInputWrapper}>
                      <span className={styles.cashCurrency}>Rs</span>
                      <input
                        type="number"
                        step="0.01"
                        className={`pos-input ${styles.cashInputHuge}`}
                        value={cashTendered}
                        onChange={(e) => setCashTendered(e.target.value)}
                        required
                        autoFocus
                        placeholder="0.00"
                      />
                    </div>

                    <div className={styles.quickBillsGrid}>
                      <button
                        type="button"
                        className="pos-btn-sm neutral"
                        onClick={() => setCashTendered(grandTotal.toString())}
                      >
                        Exact Amt
                      </button>
                      <button
                        type="button"
                        className="pos-btn-sm neutral"
                        onClick={() => setCashTendered('1000')}
                      >
                        Rs 1000
                      </button>
                      <button
                        type="button"
                        className="pos-btn-sm neutral"
                        onClick={() => setCashTendered('5000')}
                      >
                        Rs 5000
                      </button>
                    </div>
                  </div>

                  {tenderedNum > 0 && tenderedNum >= grandTotal && (
                    <div className={styles.changeDueBox}>
                      <div className={styles.changeDueLabel}>Change to Return</div>
                      <div className={styles.changeDueValue}>Rs {changeToGive.toFixed(2)}</div>
                    </div>
                  )}
                </>
              )}

              {/* 💳 CARD WORKFLOW */}
              {checkoutMode === 'card' && (
                <div className={styles.cardProcessingSection}>
                  <RiBankCardLine className={styles.cardPulsingIcon} />
                  <h3 className={styles.cardInstruction}>Awaiting Terminal Verification...</h3>
                  <p className={styles.cardSubText}>
                    Please process the charge of <b>Rs {grandTotal.toFixed(2)}</b> on the physical
                    card terminal.
                  </p>
                </div>
              )}

              <div className={styles.checkoutActionGrid}>
                <button
                  type="button"
                  className="pos-btn neutral"
                  onClick={() => {
                    setCheckoutMode('none')
                    setCashTendered('')
                  }}
                  disabled={isProcessing}
                >
                  CANCEL
                </button>
                <button
                  type="submit"
                  className="pos-btn success"
                  disabled={isProcessing || (checkoutMode === 'cash' && tenderedNum < grandTotal)}
                >
                  {isProcessing
                    ? 'PROCESSING...'
                    : checkoutMode === 'card'
                      ? '✅ PAYMENT SUCCESSFUL'
                      : 'COMPLETE SALE'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
