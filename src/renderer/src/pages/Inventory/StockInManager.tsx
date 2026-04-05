// src/renderer/src/pages/Inventory/StockInManager.tsx
import React, { useState, useEffect, useMemo, useRef } from 'react'
import Swal from 'sweetalert2'
import { Product, Supplier } from '../../types/models'
import styles from './StockInManager.module.css'

interface GRNItem {
  id: string
  productId: number
  name: string
  barcode: string
  unit: string
  qty: string
  buyPrice: string
  sellPrice: string
  discountPercent: string
  discountAmount: number
  total: number
  qtyType: string // 🚀 NEW: Track if it's kg or quantity
}

const DRAFT_STORAGE_KEY = 'jh_POS_grn_draft'

export default function StockInManager() {
  const [products, setProducts] = useState<Product[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])

  // Form States
  const [selectedSupplier, setSelectedSupplier] = useState('')
  const [invoiceNo, setInvoiceNo] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(() => new Date().toISOString().split('T')[0])
  const [grnItems, setGrnItems] = useState<GRNItem[]>([])

  // Temporary Input States
  const [searchInputValue, setSearchInputValue] = useState('')
  const [isDropdownOpen, setIsDropdownOpen] = useState(false)
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null)
  const [inputQty, setInputQty] = useState('')
  const [inputBuyPrice, setInputBuyPrice] = useState('')
  const [inputSellPrice, setInputSellPrice] = useState('')
  const [inputDiscountPercent, setInputDiscountPercent] = useState('')
  const [activeQtyType, setActiveQtyType] = useState('quantity')

  // Refs for "Excel-style" navigation
  const searchRef = useRef<HTMLInputElement>(null)
  const qtyRef = useRef<HTMLInputElement>(null)
  const buyRef = useRef<HTMLInputElement>(null)
  const sellRef = useRef<HTMLInputElement>(null)
  const discRef = useRef<HTMLInputElement>(null)
  const addBtnRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const loadData = async () => {
      try {
        // @ts-ignore
        setProducts(await window.api.getProducts())
        // @ts-ignore
        setSuppliers(await window.api.getSuppliers())
      } catch (err) {
        console.error('Data load failed', err)
      }
    }
    loadData()

    const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY)
    if (savedDraft) {
      try {
        const parsed = JSON.parse(savedDraft)
        if (parsed.selectedSupplier) setSelectedSupplier(parsed.selectedSupplier)
        if (parsed.invoiceNo) setInvoiceNo(parsed.invoiceNo)
        if (parsed.invoiceDate) setInvoiceDate(parsed.invoiceDate)
        if (parsed.grnItems) setGrnItems(parsed.grnItems)
      } catch (e) {
        console.error('Failed to parse GRN draft', e)
      }
    }
  }, [])

  useEffect(() => {
    const draftState = { selectedSupplier, invoiceNo, invoiceDate, grnItems }
    localStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(draftState))
  }, [selectedSupplier, invoiceNo, invoiceDate, grnItems])

  const handleClearDraft = async () => {
    const result = await Swal.fire({
      title: 'Clear Draft?',
      text: 'Are you sure you want to clear this draft? All items will be lost.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, clear it!'
    })

    if (result.isConfirmed) {
      setSelectedSupplier('')
      setInvoiceNo('')
      setInvoiceDate(new Date().toISOString().split('T')[0])
      setGrnItems([])
      localStorage.removeItem(DRAFT_STORAGE_KEY)
    }
  }

  const filteredProducts = useMemo(() => {
    if (!searchInputValue) return []
    const q = searchInputValue.toLowerCase()
    return products
      .filter(
        (p) =>
          p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))
      )
      .slice(0, 10)
  }, [products, searchInputValue])

  const handleSelectProduct = (prod: Product) => {
    setSelectedProductId(prod.Id)
    setSearchInputValue(prod.Name)
    setIsDropdownOpen(false)
    setInputBuyPrice(prod.BuyingPrice.toString())
    setInputSellPrice(prod.SellingPrice.toString())
    setInputDiscountPercent(prod.DiscountLimit.toString())
    setActiveQtyType(prod.QuantityType || 'quantity')
    setInputQty('1')
    setTimeout(() => qtyRef.current?.focus(), 100)
  }

  const handleKeyDown = (e: React.KeyboardEvent, nextRef: React.RefObject<any>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      nextRef.current?.focus()
    }
  }

  const handleAddItem = (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedProductId)
      return Swal.fire('Missing Product', 'Select a product first!', 'warning')

    const prod = products.find((p) => p.Id === selectedProductId)
    const qtyNum = parseFloat(inputQty) || 0
    const buyNum = parseFloat(inputBuyPrice) || 0
    const sellNum = parseFloat(inputSellPrice) || 0

    if (!prod || qtyNum <= 0 || buyNum <= 0 || sellNum <= 0) {
      return Swal.fire('Invalid Input', 'Please enter valid quantities and prices.', 'error')
    }

    // 🚀 Decimal Validation
    if (activeQtyType === 'quantity' && qtyNum % 1 !== 0) {
      return Swal.fire('Error', `Cannot add partial amounts for ${prod.Name}.`, 'error')
    }

    let discPercentNum = parseFloat(inputDiscountPercent) || 0
    const calculatedDiscountRs = parseFloat(((sellNum * discPercentNum) / 100).toFixed(2))

    const newItem: GRNItem = {
      id: Math.random().toString(36).substr(2, 9),
      productId: prod.Id,
      name: prod.Name,
      barcode: prod.Barcode || 'N/A',
      unit: prod.Unit || 'Pcs',
      qty: inputQty,
      buyPrice: inputBuyPrice,
      sellPrice: inputSellPrice,
      discountPercent: discPercentNum.toString(),
      discountAmount: calculatedDiscountRs,
      total: qtyNum * buyNum,
      qtyType: activeQtyType
    }

    setGrnItems([newItem, ...grnItems])
    setSearchInputValue('')
    setSelectedProductId(null)
    setInputBuyPrice('')
    setInputSellPrice('')
    setInputQty('')
    setInputDiscountPercent('')
    searchRef.current?.focus() // Return focus to search for next item
  }

  const handleRemoveItem = (cartId: string) => {
    setGrnItems(grnItems.filter((item) => item.id !== cartId))
  }

  const handleProcessGRN = async () => {
    if (!selectedSupplier || grnItems.length === 0 || !invoiceNo) {
      return Swal.fire(
        'Missing Details',
        'Please complete the Header details and add items.',
        'warning'
      )
    }

    const totalVal = grnItems.reduce((sum, i) => sum + i.total, 0)
    const confirmResult = await Swal.fire({
      title: 'Process GRN?',
      text: `Confirming supplier invoice for Rs ${totalVal.toFixed(2)}`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#16a34a',
      confirmButtonText: 'Yes, process it!'
    })

    if (confirmResult.isConfirmed) {
      try {
        const payload = {
          SupplierId: parseInt(selectedSupplier),
          ReferenceNo: invoiceNo,
          InvoiceDate: invoiceDate,
          Items: grnItems.map((item) => ({
            ...item,
            qty: parseFloat(item.qty),
            buyPrice: parseFloat(item.buyPrice),
            sellPrice: parseFloat(item.sellPrice),
            discountLimit: item.discountAmount
          }))
        }
        // @ts-ignore
        await window.api.processGRN(payload)
        Swal.fire('Success!', '✅ GRN Processed and stock updated.', 'success')
        localStorage.removeItem(DRAFT_STORAGE_KEY)
        setGrnItems([])
        setSelectedSupplier('')
        setInvoiceNo('')
        setInvoiceDate(new Date().toISOString().split('T')[0])
      } catch (err: any) {
        Swal.fire('Error', 'Error processing GRN.', 'error')
      }
    }
  }

  const totalGRNValue = grnItems.reduce((sum, item) => sum + item.total, 0)

  return (
    <div className={styles.container}>
      {/* 🚀 STEP 1: HEADER INFO */}
      <div className={styles.panel}>
        <div className={styles.panelHeaderRow}>
          <h2 className={styles.panelTitle}>1. Invoice Header</h2>
          {grnItems.length > 0 && <span className={styles.draftBadge}>💾 DRAFT AUTO-SAVED</span>}
        </div>

        <div className={styles.infoGrid}>
          <div className={styles.formGroup}>
            <label>Supplier / Vendor *</label>
            <select
              className="pos-input"
              value={selectedSupplier}
              onChange={(e) => setSelectedSupplier(e.target.value)}
            >
              <option value="">-- Select Vendor --</option>
              {suppliers.map((s) => (
                <option key={s.Id} value={s.Id}>
                  {s.Name}
                </option>
              ))}
            </select>
          </div>
          <div className={styles.formGroup}>
            <label>Invoice Number *</label>
            <input
              type="text"
              className="pos-input"
              placeholder="INV-001"
              value={invoiceNo}
              onChange={(e) => setInvoiceNo(e.target.value)}
            />
          </div>
          <div className={styles.formGroup}>
            <label>Received Date *</label>
            <input
              type="date"
              className="pos-input"
              value={invoiceDate}
              onChange={(e) => setInvoiceDate(e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* 🚀 STEP 2: ITEM ENTRY */}
      <div className={styles.panel} style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        <h2 className={styles.panelTitle}>2. Item Entry</h2>
        <form onSubmit={handleAddItem} className={styles.addBarGrid}>
          <div className={styles.formGroup} style={{ flex: 2 }}>
            <label>Search or Scan Product</label>
            <div className={styles.searchWrapper}>
              <input
                ref={searchRef}
                type="text"
                className="pos-input"
                placeholder="Barcode or Name..."
                value={searchInputValue}
                onChange={(e) => {
                  setSearchInputValue(e.target.value)
                  setIsDropdownOpen(true)
                }}
                onFocus={() => setIsDropdownOpen(true)}
                onBlur={() => setTimeout(() => setIsDropdownOpen(false), 200)}
                onKeyDown={(e) => handleKeyDown(e, qtyRef)}
              />
              {isDropdownOpen && filteredProducts.length > 0 && (
                <ul className={styles.dropdown}>
                  {filteredProducts.map((p) => (
                    <li key={p.Id} onClick={() => handleSelectProduct(p)}>
                      {p.Barcode} - <strong>{p.Name}</strong>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          <div className={styles.formGroup}>
            <label>Qty ({activeQtyType === 'kg' ? 'kg' : 'Pcs'})</label>
            <input
              ref={qtyRef}
              type="number"
              step={activeQtyType === 'kg' ? '0.01' : '1'}
              className="pos-input"
              value={inputQty}
              onChange={(e) => setInputQty(e.target.value)}
              onKeyDown={(e) => handleKeyDown(e, buyRef)}
              required
            />
          </div>
          <div className={styles.formGroup}>
            <label>Buy Price (Rs)</label>
            <input
              ref={buyRef}
              type="number"
              step="0.01"
              className="pos-input"
              value={inputBuyPrice}
              onChange={(e) => setInputBuyPrice(e.target.value)}
              onKeyDown={(e) => handleKeyDown(e, sellRef)}
              required
            />
          </div>
          <div className={styles.formGroup}>
            <label>Sell Price (Rs)</label>
            <input
              ref={sellRef}
              type="number"
              step="0.01"
              className="pos-input"
              value={inputSellPrice}
              onChange={(e) => setInputSellPrice(e.target.value)}
              onKeyDown={(e) => handleKeyDown(e, discRef)}
              required
            />
          </div>
          <button
            ref={addBtnRef}
            type="submit"
            className="pos-btn success"
            style={{ alignSelf: 'flex-end', minHeight: '58px' }}
          >
            + ADD
          </button>
        </form>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>ITEM NAME</th>
                <th>QTY</th>
                <th>BUY PRICE</th>
                <th>SELL PRICE</th>
                <th>MARGIN</th>
                <th>TOTAL</th>
                <th style={{ textAlign: 'center' }}>X</th>
              </tr>
            </thead>
            <tbody>
              {grnItems.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}
                  >
                    Scan or search products to build your invoice.
                  </td>
                </tr>
              ) : (
                grnItems.map((item) => {
                  const profit = parseFloat(item.sellPrice) - parseFloat(item.buyPrice)
                  const profitPercent = ((profit / parseFloat(item.buyPrice)) * 100).toFixed(1)
                  const isLoss = profit < 0
                  return (
                    <tr
                      key={item.id}
                      style={{ backgroundColor: isLoss ? '#fff1f2' : 'transparent' }}
                    >
                      <td style={{ fontWeight: 800 }}>{item.name}</td>
                      <td style={{ fontWeight: 900 }}>
                        {item.qty}{' '}
                        <span style={{ fontSize: '10px', color: 'var(--text-muted)' }}>
                          {item.unit}
                        </span>
                      </td>
                      <td>Rs {parseFloat(item.buyPrice).toFixed(2)}</td>
                      <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                        Rs {parseFloat(item.sellPrice).toFixed(2)}
                      </td>
                      <td
                        style={{
                          fontWeight: 700,
                          color: isLoss ? 'var(--action-danger)' : 'var(--brand-primary)'
                        }}
                      >
                        {profitPercent}% {isLoss && '⚠️ LOSS'}
                      </td>
                      <td style={{ fontWeight: 900 }}>Rs {item.total.toFixed(2)}</td>
                      <td style={{ textAlign: 'center' }}>
                        <button
                          className="pos-btn danger"
                          style={{ minHeight: '35px', padding: '0 10px' }}
                          onClick={() => handleRemoveItem(item.id)}
                        >
                          ✖
                        </button>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>

        {/* 🚀 FOOTER ACTIONS */}
        <div className={styles.footerRow}>
          <div className={styles.summaryInfo}>
            <div className={styles.totalLabel}>Grand Total</div>
            <div className={styles.totalValue}>Rs {totalGRNValue.toFixed(2)}</div>
          </div>
          <div style={{ display: 'flex', gap: '15px' }}>
            <button type="button" className="pos-btn neutral" onClick={handleClearDraft}>
              🗑️ CLEAR DRAFT
            </button>
            <button
              className="pos-btn success"
              onClick={handleProcessGRN}
              style={{ padding: '0 50px' }}
            >
              ✅ PROCESS INVOICE
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
