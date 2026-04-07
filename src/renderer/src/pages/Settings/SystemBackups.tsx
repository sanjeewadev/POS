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
          {/* 🚀 Replaced with Global Title Class */}
          <h2 className="pos-page-title">System Preferences & Data Vault</h2>
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
                <div className={styles.printerRow}>
                  <select
                    className={`pos-input ${styles.printerSelect}`}
                    value={selectedPrinter}
                    onChange={(e) => setSelectedPrinter(e.target.value)}
                  >
                    <option value="">-- Select a Printer --</option>
                    {printers.map((p, idx) => (
                      <option key={idx} value={p.name}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                  <button className={`pos-btn neutral ${styles.refreshBtn}`} onClick={loadPrinters}>
                    <RiRefreshLine size={18} /> Refresh
                  </button>
                </div>
                <button
                  className={`pos-btn success ${styles.savePrinterBtn}`}
                  onClick={handleSavePrinter}
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
              <div className={`${styles.cardBody} ${styles.cardBodyFlex}`}>
                <button
                  className={`pos-btn neutral ${styles.vaultBtn}`}
                  onClick={handleBackup}
                  disabled={isProcessing}
                >
                  <RiDownloadCloud2Line size={32} color="#0284c7" />
                  <span className={styles.vaultBtnText}>EXPORT BACKUP</span>
                </button>
                <button
                  className={`pos-btn neutral ${styles.vaultBtn}`}
                  onClick={handleRestore}
                  disabled={isProcessing}
                >
                  <RiUploadCloud2Line size={32} color="#64748b" />
                  <span className={styles.vaultBtnText}>RESTORE DATA</span>
                </button>
              </div>
            </div>

            {/* MODULE 3: DANGER ZONE */}
            <div className={`${styles.card} ${styles.dangerCard}`}>
              <div className={styles.cardHeader}>
                <RiAlertLine className={styles.headerIcon} color="#dc2626" />
                <div>
                  <h3 className={styles.dangerTitle}>DANGER ZONE: Factory Reset</h3>
                  <p>Permanently wipe all data. Only Admin remains.</p>
                </div>
              </div>
              <div className={styles.cardBody}>
                {!showResetConfirm ? (
                  <button
                    className={`pos-btn danger ${styles.dangerBtnOutline}`}
                    onClick={() => setShowResetConfirm(true)}
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
                      className={`pos-input ${styles.dangerInput}`}
                      value={resetText}
                      onChange={(e) => setResetText(e.target.value)}
                    />
                    <div className={styles.confirmActions}>
                      <button
                        className={`pos-btn neutral ${styles.flexBtn}`}
                        onClick={() => setShowResetConfirm(false)}
                      >
                        CANCEL
                      </button>
                      <button
                        className={`pos-btn danger ${styles.flexBtn}`}
                        onClick={handleFactoryReset}
                        disabled={resetText !== 'DELETE ALL DATA'}
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
