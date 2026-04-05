// src/renderer/src/pages/Settings/SystemBackups.tsx
import { useState, useEffect } from 'react'
import Swal from 'sweetalert2'
import {
  RiPrinterLine,
  RiDatabase2Line,
  RiAlertLine,
  RiDownloadCloud2Line,
  RiUploadCloud2Line,
  RiRefreshLine
} from 'react-icons/ri'
import styles from './SystemBackups.module.css'

export default function SystemBackups() {
  const [printers, setPrinters] = useState<any[]>([])
  const [selectedPrinter, setSelectedPrinter] = useState('')
  const [isProcessing, setIsProcessing] = useState(false)
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
      if (result?.success)
        Swal.fire('Backup Saved', 'Database backup saved successfully!', 'success')
    } catch (err: any) {
      Swal.fire('Backup Failed', err.message, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleRestore = async () => {
    const confirmResult = await Swal.fire({
      title: 'CRITICAL WARNING',
      text: 'Restoring a backup will permanently overwrite ALL current data. Proceed?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      confirmButtonText: 'Yes, OVERWRITE!'
    })
    if (!confirmResult.isConfirmed) return
    setIsProcessing(true)
    try {
      // @ts-ignore
      await window.api.importDatabase()
    } catch (err: any) {
      Swal.fire('Restore Failed', err.message, 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleFactoryReset = async () => {
    if (resetText !== 'DELETE ALL DATA') return Swal.fire('Error', 'Type correctly!', 'error')
    setIsProcessing(true)
    try {
      // @ts-ignore
      await window.api.factoryReset()
    } catch (err: any) {
      Swal.fire('Failed', err.message, 'error')
      setIsProcessing(false)
    }
  }

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <h2 className={styles.pageTitle}>System Preferences & Data Vault</h2>
        </div>

        <div className={styles.panelBody}>
          <div className={styles.grid}>
            {/* MODULE 1: PRINTER */}
            <div className={styles.card}>
              <div className={styles.cardHeader}>
                <RiPrinterLine className={styles.headerIcon} />
                <div>
                  <h3>Receipt Printer Configuration</h3>
                  <p>Select the default thermal printer for receipts.</p>
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
                        {p.name}
                      </option>
                    ))}
                  </select>
                  <button className="pos-btn neutral" onClick={loadPrinters} style={{ gap: '8px' }}>
                    <RiRefreshLine size={18} /> Refresh
                  </button>
                </div>
                <button
                  className="pos-btn success"
                  onClick={handleSavePrinter}
                  style={{ width: '100%', marginTop: '20px', fontWeight: 700 }}
                >
                  SAVE POS PRINTER
                </button>
              </div>
            </div>

            {/* MODULE 2: DATABASE */}
            <div className={styles.card}>
              <div className={styles.cardHeader}>
                <RiDatabase2Line className={styles.headerIcon} />
                <div>
                  <h3>Database Vault</h3>
                  <p>Securely backup or restore your financial data.</p>
                </div>
              </div>
              <div className={styles.cardBody} style={{ display: 'flex', gap: '15px' }}>
                <button
                  className="pos-btn neutral"
                  onClick={handleBackup}
                  disabled={isProcessing}
                  style={{ flex: 1, height: '100px', flexDirection: 'column', gap: '8px' }}
                >
                  <RiDownloadCloud2Line size={32} color="#0284c7" />
                  <span style={{ fontWeight: 700 }}>EXPORT BACKUP</span>
                </button>
                <button
                  className="pos-btn neutral"
                  onClick={handleRestore}
                  disabled={isProcessing}
                  style={{ flex: 1, height: '100px', flexDirection: 'column', gap: '8px' }}
                >
                  <RiUploadCloud2Line size={32} color="#64748b" />
                  <span style={{ fontWeight: 700 }}>RESTORE DATA</span>
                </button>
              </div>
            </div>

            {/* MODULE 3: DANGER ZONE */}
            <div className={`${styles.card} ${styles.dangerCard}`}>
              <div className={styles.cardHeader}>
                <RiAlertLine className={styles.headerIcon} color="#dc2626" />
                <div>
                  <h3 style={{ color: '#dc2626' }}>DANGER ZONE: Factory Reset</h3>
                  <p>Permanently wipe all data. Only Admin remains.</p>
                </div>
              </div>
              <div className={styles.cardBody}>
                {!showResetConfirm ? (
                  <button
                    className="pos-btn danger"
                    onClick={() => setShowResetConfirm(true)}
                    style={{
                      width: '100%',
                      background: 'white',
                      color: '#dc2626',
                      border: '1px solid #dc2626',
                      fontWeight: 700
                    }}
                  >
                    INITIATE FACTORY RESET
                  </button>
                ) : (
                  <div className={styles.resetConfirmBox}>
                    <label>
                      To proceed, type <strong>DELETE ALL DATA</strong>:
                    </label>
                    <input
                      type="text"
                      className="pos-input"
                      style={{ textAlign: 'center', fontWeight: 900, color: '#dc2626' }}
                      value={resetText}
                      onChange={(e) => setResetText(e.target.value)}
                    />
                    <div style={{ display: 'flex', gap: '10px', marginTop: '15px' }}>
                      <button
                        className="pos-btn neutral"
                        onClick={() => setShowResetConfirm(false)}
                        style={{ flex: 1 }}
                      >
                        CANCEL
                      </button>
                      <button
                        className="pos-btn danger"
                        onClick={handleFactoryReset}
                        disabled={resetText !== 'DELETE ALL DATA'}
                        style={{ flex: 1 }}
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
      </div>
    </div>
  )
}
