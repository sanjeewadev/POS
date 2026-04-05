// src/renderer/src/pages/Settings/ShopSettings.tsx
import { useState, useEffect } from 'react'
import Swal from 'sweetalert2'
import styles from './ShopSettings.module.css'

export default function ShopSettings() {
  const [shopName, setShopName] = useState('My POS System')
  const [shopAddress, setShopAddress] = useState('')
  const [shopPhone, setShopPhone] = useState('')
  const [receiptFooter, setReceiptFooter] = useState('Thank you for your business!')

  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    const loadSettings = async () => {
      setIsLoading(true)
      try {
        // @ts-ignore
        const data = await window.api.getSettings()
        if (data) {
          setShopName(data.ShopName || '')
          setShopAddress(data.ShopAddress || '')
          setShopPhone(data.ShopPhone || '')
          setReceiptFooter(data.ReceiptFooter || '')
        }
      } catch (error) {
        console.error('Failed to load shop settings', error)
      } finally {
        setIsLoading(false)
      }
    }
    loadSettings()
  }, [])

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()

    if (!shopName.trim()) {
      return Swal.fire('Required Field', 'Your Shop Name cannot be empty.', 'warning')
    }

    setIsSaving(true)
    try {
      const payload = {
        ShopName: shopName,
        ShopAddress: shopAddress,
        ShopPhone: shopPhone,
        ReceiptFooter: receiptFooter
      }

      // @ts-ignore
      await window.api.updateSettings(payload)

      Swal.fire({
        title: 'Settings Saved!',
        text: 'Your receipt headers and shop details have been updated.',
        icon: 'success',
        timer: 1500,
        showConfirmButton: false
      })
    } catch (error: any) {
      Swal.fire('Error', 'Failed to save settings: ' + error.message, 'error')
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) {
    return <div className={styles.loadingState}>Loading Settings...</div>
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h2 className={styles.title}>Store Configuration</h2>
        <p className={styles.subtitle}>
          Update your shop details and customize how your receipts are printed.
        </p>
      </div>

      <div className={styles.splitLayout}>
        {/* --- LEFT: THE ENTRY FORM --- */}
        <div className={styles.formPanel}>
          <form onSubmit={handleSave} className={styles.settingsForm}>
            <div className={styles.formGroup}>
              <label>Shop / Business Name *</label>
              <input
                type="text"
                className="pos-input"
                value={shopName}
                onChange={(e) => setShopName(e.target.value)}
                placeholder="e.g. Universal Hardware"
                required
              />
            </div>

            <div className={styles.formGroup}>
              <label>Business Address</label>
              <input
                type="text"
                className="pos-input"
                value={shopAddress}
                onChange={(e) => setShopAddress(e.target.value)}
                placeholder="e.g. 123 Main Street, City"
              />
            </div>

            <div className={styles.formGroup}>
              <label>Contact Phone Number</label>
              <input
                type="text"
                className="pos-input"
                value={shopPhone}
                onChange={(e) => setShopPhone(e.target.value)}
                placeholder="e.g. 011-2345678"
              />
            </div>

            <div className={styles.formGroup}>
              <label>Receipt Footer Message</label>
              <input
                type="text"
                className="pos-input"
                value={receiptFooter}
                onChange={(e) => setReceiptFooter(e.target.value)}
                placeholder="e.g. Thank you for shopping with us! Come again."
              />
            </div>

            <div className={styles.actionRow}>
              <button
                type="submit"
                className="pos-btn success"
                style={{ width: '100%', marginTop: '20px' }}
                disabled={isSaving}
              >
                {isSaving ? 'SAVING...' : 'SAVE STORE CONFIGURATION'}
              </button>
            </div>
          </form>
        </div>

        {/* --- RIGHT: LIVE RECEIPT PREVIEW --- */}
        <div className={styles.previewPanel}>
          <div className={styles.previewHeader}>Live Receipt Preview</div>
          <div className={styles.receiptPaper}>
            <div className={styles.receiptTop}>
              <h3 className={styles.receiptShopName}>{shopName || 'Your Shop Name'}</h3>
              <div className={styles.receiptAddress}>
                {shopAddress || '123 Business Road, City'}
              </div>
              <div className={styles.receiptPhone}>{shopPhone || 'Tel: 000-0000000'}</div>
            </div>

            <div className={styles.receiptDivider}>--------------------------------</div>

            <div className={styles.receiptMeta}>
              <div>Date: {new Date().toLocaleDateString()}</div>
              <div>
                Time: {new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </div>
              <div>Inv No: INV-10204</div>
            </div>

            <div className={styles.receiptDivider}>--------------------------------</div>

            <table className={styles.receiptTable}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'left' }}>Item</th>
                  <th style={{ textAlign: 'center' }}>Qty</th>
                  <th style={{ textAlign: 'right' }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>Example Product A</td>
                  <td style={{ textAlign: 'center' }}>2</td>
                  <td style={{ textAlign: 'right' }}>150.00</td>
                </tr>
                <tr>
                  <td>Example Product B</td>
                  <td style={{ textAlign: 'center' }}>1</td>
                  <td style={{ textAlign: 'right' }}>450.00</td>
                </tr>
              </tbody>
            </table>

            <div className={styles.receiptDivider}>--------------------------------</div>

            <div className={styles.receiptTotals}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontWeight: 900,
                  fontSize: '16px'
                }}
              >
                <span>TOTAL</span>
                <span>Rs 600.00</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '5px' }}>
                <span>CASH TENDERED</span>
                <span>Rs 1000.00</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '5px' }}>
                <span>CHANGE</span>
                <span>Rs 400.00</span>
              </div>
            </div>

            <div className={styles.receiptDivider}>--------------------------------</div>

            <div className={styles.receiptFooterText}>
              {receiptFooter || 'Thank you for your business!'}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
