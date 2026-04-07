// src/renderer/src/pages/Settings/ShopSettings.tsx
import { useState, useEffect } from 'react'
import Swal from 'sweetalert2'
import { RiStore2Line, RiMapPin2Line, RiPhoneLine, RiFontSize } from 'react-icons/ri'
import styles from './ShopSettings.module.css'

export default function ShopSettings() {
  // Started with empty strings so it doesn't flash default text before loading
  const [shopName, setShopName] = useState('')
  const [shopAddress, setShopAddress] = useState('')
  const [shopPhone, setShopPhone] = useState('')
  const [receiptFooter, setReceiptFooter] = useState('')

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
    if (!shopName.trim()) return Swal.fire('Required', 'Shop Name is required.', 'warning')

    setIsSaving(true)
    try {
      // 🚀 THE FIX: Capitalized the keys to perfectly match systemRepo.ts!
      const payload = {
        ShopName: shopName,
        ShopAddress: shopAddress,
        ShopPhone: shopPhone,
        ReceiptFooter: receiptFooter
      }

      // @ts-ignore
      await window.api.updateSettings(payload)
      Swal.fire({ title: 'Saved!', icon: 'success', timer: 1500, showConfirmButton: false })
    } catch (error: any) {
      Swal.fire('Error', error.message, 'error')
    } finally {
      setIsSaving(false)
    }
  }

  if (isLoading) return <div className={styles.loadingState}>Loading...</div>

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <h2 className="pos-page-title">Store Configuration</h2>
        </div>

        <div className={styles.panelBody}>
          <div className={styles.splitLayout}>
            {/* LEFT: FORM */}
            <div className={styles.formPanel}>
              <form onSubmit={handleSave} className={styles.settingsForm}>
                <div className={styles.formGroup}>
                  <label>
                    <RiStore2Line /> Shop / Business Name *
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={shopName}
                    onChange={(e) => setShopName(e.target.value)}
                    required
                  />
                </div>

                <div className={styles.formGroup}>
                  <label>
                    <RiMapPin2Line /> Business Address
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={shopAddress}
                    onChange={(e) => setShopAddress(e.target.value)}
                  />
                </div>

                <div className={styles.formGroup}>
                  <label>
                    <RiPhoneLine /> Contact Phone Number
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={shopPhone}
                    onChange={(e) => setShopPhone(e.target.value)}
                  />
                </div>

                <div className={styles.formGroup}>
                  <label>
                    <RiFontSize /> Receipt Footer Message
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={receiptFooter}
                    onChange={(e) => setReceiptFooter(e.target.value)}
                  />
                </div>

                <button
                  type="submit"
                  className={`pos-btn success ${styles.saveBtn}`}
                  disabled={isSaving}
                >
                  {isSaving ? 'SAVING...' : 'SAVE STORE CONFIGURATION'}
                </button>
              </form>
            </div>

            {/* RIGHT: RECEIPT PREVIEW */}
            <div className={styles.previewPanel}>
              <div className={styles.previewHeader}>Live Receipt Preview</div>
              <div className={styles.receiptPaper}>
                <div className={styles.receiptTop}>
                  <h3 className={styles.receiptShopName}>{shopName || 'SHOP NAME'}</h3>
                  <div className={styles.receiptAddress}>{shopAddress || 'Business Address'}</div>
                  <div className={styles.receiptPhone}>{shopPhone || 'Phone Number'}</div>
                </div>

                <div className={styles.receiptDivider}>--------------------------------</div>
                <div className={styles.receiptMeta}>
                  <div>Date: {new Date().toLocaleDateString()}</div>
                  <div>Inv No: INV-10204</div>
                </div>
                <div className={styles.receiptDivider}>--------------------------------</div>

                <table className={styles.receiptTable}>
                  <thead>
                    <tr>
                      <th className={styles.textLeft}>Item</th>
                      <th className={styles.textRight}>Amount</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>Sample Product</td>
                      <td className={styles.textRight}>500.00</td>
                    </tr>
                  </tbody>
                </table>

                <div className={styles.receiptDivider}>--------------------------------</div>
                <div className={styles.receiptTotals}>
                  <div className={styles.totalRow}>
                    <span>TOTAL</span>
                    <span>Rs 500.00</span>
                  </div>
                </div>
                <div className={styles.receiptDivider}>--------------------------------</div>
                <div className={styles.receiptFooterText}>{receiptFooter}</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
