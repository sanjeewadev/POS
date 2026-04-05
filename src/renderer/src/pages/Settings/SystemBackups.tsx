// src/renderer/src/pages/Settings/SystemBackups.tsx
import { useState, useEffect } from 'react'
import Swal from 'sweetalert2'
import styles from './SystemBackups.module.css'

export default function SystemBackups() {
  const [printers, setPrinters] = useState<any[]>([])
  const [selectedPrinter, setSelectedPrinter] = useState('')
  const [isProcessing, setIsProcessing] = useState(false)

  // Danger Zone State
  const [resetText, setResetText] = useState('')
  const [showResetConfirm, setShowResetConfirm] = useState(false)

  useEffect(() => {
    loadPrinters()
    const savedPrinter = localStorage.getItem('pos_printer_name')
    if (savedPrinter) setSelectedPrinter(savedPrinter)
  }, [])

  const loadPrinters = async () => {
    try {
      // @ts-ignore
      const printerList = await window.api.getPrinters()
      setPrinters(printerList || [])
    } catch (err) {
      console.error('Failed to load printers:', err)
    }
  }

  const handleSavePrinter = () => {
    localStorage.setItem('pos_printer_name', selectedPrinter)
    Swal.fire({
      title: 'Saved!',
      text: `Printer config updated: ${selectedPrinter}`,
      icon: 'success',
      timer: 1500
    })
  }

  const handleBackup = async () => {
    setIsProcessing(true)
    try {
      // @ts-ignore
      const result = await window.api.exportDatabase()
      if (result && result.success) {
        Swal.fire('Backup Saved', '✅ Database backup saved successfully!', 'success')
      }
    } catch (err: any) {
      Swal.fire('Backup Failed', `❌ ${err.message}`, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleRestore = async () => {
    const confirmResult = await Swal.fire({
      title: '🚨 CRITICAL WARNING',
      text: 'Restoring a backup will permanently overwrite ALL current data in the system. The application will restart automatically.\n\nAre you sure you want to proceed?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, OVERWRITE my data!'
    })

    if (!confirmResult.isConfirmed) return

    setIsProcessing(true)
    try {
      // @ts-ignore
      const result = await window.api.importDatabase()
      if (result && !result.success && !result.canceled) {
        Swal.fire('Restore Failed', '❌ Restore failed. Check logs.', 'error')
      }
    } catch (err: any) {
      Swal.fire('Restore Failed', `❌ ${err.message}`, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleFactoryReset = async () => {
    if (resetText !== 'DELETE ALL DATA') {
      return Swal.fire(
        'Action Denied',
        'You must type EXACTLY "DELETE ALL DATA" to proceed.',
        'error'
      )
    }

    setIsProcessing(true)
    try {
      // @ts-ignore
      await window.api.factoryReset()
    } catch (err: any) {
      Swal.fire('Factory Reset Failed', `❌ ${err.message}`, 'error')
      setIsProcessing(false)
    }
  }

  return (
    <div className={styles.container}>
      <h2 className={styles.pageTitle}>SYSTEM PREFERENCES & DATA VAULT</h2>

      <div className={styles.grid}>
        {/* --- MODULE 1: POS (PRINTERS) --- */}
        <div className={styles.card}>
          <div className={styles.cardHeader}>
            <div style={{ fontSize: '24px' }}>🖨️</div>
            <div>
              <h3>Receipt Printer Configuration</h3>
              <p>Select the default thermal printer for point-of-sale receipts.</p>
            </div>
          </div>
          <div className={styles.cardBody}>
            <div style={{ display: 'flex', gap: '15px' }}>
              <select
                className="pos-input"
                value={selectedPrinter}
                onChange={(e) => setSelectedPrinter(e.target.value)}
                style={{ flex: 1 }}
              >
                <option value="">-- Select a Printer --</option>
                {printers.map((p, idx) => (
                  <option key={idx} value={p.name}>
                    {p.name} {p.isDefault ? '(Default OS Printer)' : ''}
                  </option>
                ))}
              </select>
              <button
                className="pos-btn neutral"
                onClick={loadPrinters}
                style={{ padding: '0 20px', minHeight: '50px' }}
              >
                🔄 Refresh
              </button>
            </div>
            <button
              className="pos-btn success"
              onClick={handleSavePrinter}
              style={{ width: '100%', marginTop: '20px' }}
            >
              SAVE POS PRINTER
            </button>
          </div>
        </div>

        {/* --- MODULE 2: DATABASE BACKUP & RESTORE --- */}
        <div className={styles.card}>
          <div className={styles.cardHeader}>
            <div style={{ fontSize: '24px' }}>💾</div>
            <div>
              <h3>Database Vault</h3>
              <p>Securely backup your financial data to a USB drive, or restore an old backup.</p>
            </div>
          </div>
          <div className={styles.cardBody} style={{ display: 'flex', gap: '15px' }}>
            <button
              className="pos-btn neutral"
              onClick={handleBackup}
              disabled={isProcessing}
              style={{ flex: 1, flexDirection: 'column', minHeight: '120px', gap: '10px' }}
            >
              <span style={{ fontSize: '32px' }}>📥</span>
              <span style={{ fontSize: '16px' }}>EXPORT BACKUP</span>
            </button>
            <button
              className="pos-btn neutral"
              onClick={handleRestore}
              disabled={isProcessing}
              style={{ flex: 1, flexDirection: 'column', minHeight: '120px', gap: '10px' }}
            >
              <span style={{ fontSize: '32px' }}>📤</span>
              <span style={{ fontSize: '16px' }}>RESTORE DATA</span>
            </button>
          </div>
        </div>

        {/* --- MODULE 3: DANGER ZONE (FACTORY RESET) --- */}
        <div className={`${styles.card} ${styles.dangerCard}`}>
          <div className={styles.cardHeader}>
            <div style={{ fontSize: '24px' }}>🚨</div>
            <div>
              <h3 style={{ color: 'var(--action-danger)' }}>DANGER ZONE: Factory Reset</h3>
              <p>
                Permanently wipe ALL inventory, suppliers, sales, and credit logs. Only Admin
                accounts will remain.
              </p>
            </div>
          </div>
          <div className={styles.cardBody}>
            {!showResetConfirm ? (
              <button
                className="pos-btn danger"
                onClick={() => setShowResetConfirm(true)}
                style={{
                  width: '100%',
                  background: 'transparent',
                  color: 'var(--action-danger)',
                  border: '2px solid var(--action-danger)'
                }}
              >
                INITIATE FACTORY RESET
              </button>
            ) : (
              <div className={styles.resetConfirmBox}>
                <label
                  style={{
                    display: 'block',
                    fontSize: '14px',
                    fontWeight: 800,
                    marginBottom: '10px'
                  }}
                >
                  To proceed, type <strong>DELETE ALL DATA</strong> below:
                </label>
                <input
                  type="text"
                  className="pos-input"
                  style={{ textAlign: 'center', fontWeight: 900, color: 'var(--action-danger)' }}
                  placeholder="DELETE ALL DATA"
                  value={resetText}
                  onChange={(e) => setResetText(e.target.value)}
                />
                <div style={{ display: 'flex', gap: '15px', marginTop: '15px' }}>
                  <button
                    className="pos-btn neutral"
                    style={{ flex: 1 }}
                    onClick={() => {
                      setShowResetConfirm(false)
                      setResetText('')
                    }}
                  >
                    CANCEL
                  </button>
                  <button
                    className="pos-btn danger"
                    style={{ flex: 1 }}
                    onClick={handleFactoryReset}
                    disabled={resetText !== 'DELETE ALL DATA' || isProcessing}
                  >
                    CONFIRM PURGE
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
