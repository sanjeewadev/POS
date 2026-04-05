// src/renderer/src/pages/Inventory/SupplierManager.tsx
import React, { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Supplier } from '../../types/models'
import styles from './SupplierManager.module.css'

export default function SupplierManager() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [searchQuery, setSearchQuery] = useState('')

  // 🚀 SMART FORM STATE
  const [editingSupplierId, setEditingSupplierId] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')

  // --- MODAL STATES ---
  const [supplierProfile, setSupplierProfile] = useState<Supplier | null>(null)
  const [supplierBills, setSupplierBills] = useState<any[]>([])

  const [viewingBillDetails, setViewingBillDetails] = useState<any | null>(null)
  const [billItems, setBillItems] = useState<any[]>([])

  const loadData = async () => {
    try {
      // @ts-ignore
      setSuppliers(await window.api.getSuppliers())
    } catch (err) {
      console.error('Failed to load suppliers', err)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  const handleClear = () => {
    setEditingSupplierId(null)
    setName('')
    setPhone('')
  }

  const handleEditClick = (sup: Supplier) => {
    setEditingSupplierId(sup.Id)
    setName(sup.Name)
    setPhone(sup.Phone || '')
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()

    const safeName = name.trim()
    const safePhone = phone.trim()

    if (!safeName) {
      Swal.fire('Error', 'Company name is required!', 'error')
      return
    }

    try {
      if (editingSupplierId) {
        // @ts-ignore
        await window.api.updateSupplier({ Id: editingSupplierId, Name: safeName, Phone: safePhone })
        Swal.fire({ title: 'Updated!', icon: 'success', timer: 1500, showConfirmButton: false })
      } else {
        // @ts-ignore
        await window.api.addSupplier({ Name: safeName, Phone: safePhone })
        Swal.fire({ title: 'Saved!', icon: 'success', timer: 1500, showConfirmButton: false })
      }
      handleClear()
      loadData()
    } catch (err: any) {
      if (err.message && err.message.includes('UNIQUE constraint failed')) {
        Swal.fire('Duplicate', '🛑 A supplier with this company name already exists.', 'warning')
      } else {
        Swal.fire('Error', 'Error saving supplier.', 'error')
      }
    }
  }

  const handleDelete = async (id: number) => {
    const confirmResult = await Swal.fire({
      title: 'Are you sure?',
      text: 'Do you want to delete this supplier?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#64748b',
      confirmButtonText: 'Yes, delete it!'
    })

    if (confirmResult.isConfirmed) {
      try {
        // @ts-ignore
        await window.api.deleteSupplier(id)
        loadData()
        Swal.fire('Deleted!', '✅ Supplier deleted successfully.', 'success')
      } catch (err: any) {
        if (err.message && err.message.includes('FOREIGN KEY constraint failed')) {
          Swal.fire(
            'Action Denied',
            '🛑 This supplier has invoice history!\n\nYou cannot delete a supplier who has provided stock. You can edit their name instead.',
            'error'
          )
        } else {
          Swal.fire('Error', 'Error deleting supplier.', 'error')
        }
      }
    }
  }

  const handleOpenProfile = async (sup: Supplier) => {
    setSupplierProfile(sup)
    try {
      // @ts-ignore
      const bills = await window.api.getSupplierInvoices(sup.Id)
      setSupplierBills(bills || [])
    } catch (err) {
      setSupplierBills([])
    }
  }

  const handleOpenBillDetails = async (bill: any) => {
    setViewingBillDetails(bill)
    try {
      // @ts-ignore
      const items = await window.api.getInvoiceItems(bill.Id)
      setBillItems(items || [])
    } catch (err) {
      setBillItems([])
    }
  }

  const displayedSuppliers = useMemo(() => {
    const q = searchQuery.toLowerCase()
    return suppliers.filter(
      (s) => s.Name.toLowerCase().includes(q) || (s.Phone && s.Phone.toLowerCase().includes(q))
    )
  }, [suppliers, searchQuery])

  return (
    <div className={styles.container}>
      {/* --- LEFT PANEL: MAIN SUPPLIER TABLE --- */}
      <div className={styles.leftPanel}>
        <div className={styles.headerRow}>
          <h2 className={styles.panelTitle}>SUPPLIER DATABASE</h2>
          <input
            type="text"
            className="pos-input"
            placeholder="Search by Name or Phone..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ width: '300px' }}
          />
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>COMPANY NAME</th>
                <th>PHONE NUMBER</th>
                <th style={{ textAlign: 'right' }}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {displayedSuppliers.length === 0 ? (
                <tr>
                  <td
                    colSpan={3}
                    style={{ textAlign: 'center', padding: '50px', color: 'var(--text-muted)' }}
                  >
                    No suppliers found. Add a new vendor on the right.
                  </td>
                </tr>
              ) : (
                displayedSuppliers.map((sup) => (
                  <tr key={sup.Id}>
                    <td style={{ fontWeight: 800, fontSize: '16px' }}>{sup.Name}</td>
                    <td
                      style={{
                        fontFamily: 'monospace',
                        fontSize: '15px',
                        color: 'var(--text-muted)'
                      }}
                    >
                      {sup.Phone || '-'}
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
                        onClick={() => handleOpenProfile(sup)}
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                      >
                        INFO
                      </button>
                      <button
                        className="pos-btn warning"
                        onClick={() => handleEditClick(sup)}
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                      >
                        EDIT
                      </button>
                      <button
                        className="pos-btn danger"
                        onClick={() => handleDelete(sup.Id)}
                        style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                      >
                        DEL
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* --- RIGHT PANEL: SMART FORM --- */}
      <div className={styles.rightPanel}>
        <form
          onSubmit={handleSave}
          style={{ display: 'flex', flexDirection: 'column', height: '100%' }}
        >
          <div className={styles.formHeader}>
            <h2
              className={styles.panelTitle}
              style={{ color: editingSupplierId ? 'var(--action-warning)' : 'var(--text-main)' }}
            >
              {editingSupplierId ? 'EDIT SUPPLIER' : 'ADD SUPPLIER'}
            </h2>
          </div>

          <div className={styles.formBody}>
            <div style={{ marginBottom: '20px' }}>
              <label
                style={{
                  fontSize: '12px',
                  fontWeight: 800,
                  color: 'var(--text-muted)',
                  textTransform: 'uppercase'
                }}
              >
                Company / Supplier Name *
              </label>
              <input
                type="text"
                className="pos-input"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoFocus
              />
            </div>
            <div style={{ marginBottom: '20px' }}>
              <label
                style={{
                  fontSize: '12px',
                  fontWeight: 800,
                  color: 'var(--text-muted)',
                  textTransform: 'uppercase'
                }}
              >
                Phone Number (Optional)
              </label>
              <input
                type="text"
                className="pos-input"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
              />
            </div>
          </div>

          <div className={styles.formFooter}>
            <button type="button" className="pos-btn neutral" onClick={handleClear}>
              {editingSupplierId ? 'CANCEL' : 'CLEAR'}
            </button>
            <button
              type="submit"
              className={`pos-btn ${editingSupplierId ? 'warning' : 'success'}`}
            >
              {editingSupplierId ? 'UPDATE SUPPLIER' : 'SAVE SUPPLIER'}
            </button>
          </div>
        </form>
      </div>

      {/* ========================================= */}
      {/* MODAL 1: MASSIVE UNIFIED PROFILE          */}
      {/* ========================================= */}
      {supplierProfile && !viewingBillDetails && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '24px', color: 'var(--text-main)' }}>
                  {supplierProfile.Name}
                </h2>
                <div
                  style={{
                    fontSize: '13px',
                    color: 'var(--text-muted)',
                    marginTop: '4px',
                    fontWeight: 600
                  }}
                >
                  Phone: {supplierProfile.Phone || 'N/A'} | System ID: #{supplierProfile.Id}
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
                  Total GRN Invoices
                </div>
                <div style={{ fontSize: '28px', fontWeight: 900, color: 'var(--brand-primary)' }}>
                  {supplierBills.length}
                </div>
              </div>
            </div>

            <div className={styles.modalBody}>
              <h3
                style={{
                  fontSize: '15px',
                  marginBottom: '15px',
                  color: 'var(--text-main)',
                  textTransform: 'uppercase',
                  fontWeight: 800
                }}
              >
                Purchase History (GRN Bills)
              </h3>
              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>INVOICE NO</th>
                      <th>DATE</th>
                      <th>TOTAL AMOUNT</th>
                      <th style={{ textAlign: 'right' }}>ACTION</th>
                    </tr>
                  </thead>
                  <tbody>
                    {supplierBills.length === 0 ? (
                      <tr>
                        <td
                          colSpan={4}
                          style={{
                            textAlign: 'center',
                            padding: '40px',
                            color: 'var(--text-muted)'
                          }}
                        >
                          No bills found for this supplier yet.
                        </td>
                      </tr>
                    ) : (
                      supplierBills.map((bill) => (
                        <tr key={bill.Id}>
                          <td style={{ fontWeight: 800 }}>{bill.BillNumber}</td>
                          <td style={{ color: 'var(--text-muted)' }}>
                            {new Date(bill.Date).toLocaleString()}
                          </td>
                          <td style={{ fontWeight: 900, color: 'var(--action-success)' }}>
                            Rs {bill.TotalAmount.toFixed(2)}
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <button
                              className="pos-btn neutral"
                              onClick={() => handleOpenBillDetails(bill)}
                              style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                            >
                              VIEW ITEMS
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
            <div className={styles.modalFooter}>
              <button className="pos-btn neutral" onClick={() => setSupplierProfile(null)}>
                CLOSE PROFILE
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ========================================= */}
      {/* MODAL 2: SPECIFIC BILL DETAILS            */}
      {/* ========================================= */}
      {viewingBillDetails && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <div>
                <h2 style={{ margin: 0, fontSize: '20px', color: 'var(--text-main)' }}>
                  Invoice: {viewingBillDetails.BillNumber}
                </h2>
                <p
                  style={{
                    margin: '4px 0 0 0',
                    fontSize: '13px',
                    color: 'var(--text-muted)',
                    fontWeight: 600
                  }}
                >
                  Received Date: {new Date(viewingBillDetails.Date).toLocaleString()}
                </p>
              </div>
              <div
                style={{
                  textAlign: 'right',
                  background: '#f0fdf4',
                  padding: '10px 20px',
                  borderRadius: '8px',
                  border: '1px solid #bbf7d0'
                }}
              >
                <p
                  style={{
                    margin: 0,
                    fontSize: '12px',
                    color: '#16a34a',
                    fontWeight: 800,
                    textTransform: 'uppercase'
                  }}
                >
                  Total Invoice Cost
                </p>
                <h2 style={{ margin: 0, fontSize: '24px', color: '#15803d', fontWeight: 900 }}>
                  Rs {viewingBillDetails.TotalAmount.toFixed(2)}
                </h2>
              </div>
            </div>

            <div className={styles.modalBody}>
              <h3
                style={{
                  fontSize: '14px',
                  marginBottom: '15px',
                  textTransform: 'uppercase',
                  fontWeight: 800
                }}
              >
                Detailed Item Breakdown
              </h3>
              <div className={styles.tableWrapper}>
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>SKU</th>
                      <th>PRODUCT NAME</th>
                      <th>RECEIVED QTY</th>
                      <th>UNIT COST</th>
                      <th>SET SELLING PRICE</th>
                      <th style={{ textAlign: 'right' }}>LINE COST TOTAL</th>
                    </tr>
                  </thead>
                  <tbody>
                    {billItems.length === 0 ? (
                      <tr>
                        <td
                          colSpan={6}
                          style={{
                            textAlign: 'center',
                            padding: '40px',
                            color: 'var(--text-muted)'
                          }}
                        >
                          No items found for this invoice.
                        </td>
                      </tr>
                    ) : (
                      billItems.map((item, idx) => (
                        <tr key={idx}>
                          <td
                            style={{
                              fontSize: '12px',
                              color: 'var(--text-muted)',
                              fontFamily: 'monospace'
                            }}
                          >
                            {item.Barcode || 'N/A'}
                          </td>
                          <td style={{ fontWeight: 800 }}>{item.ProductName}</td>
                          <td style={{ fontWeight: 900 }}>
                            {item.InitialQuantity}{' '}
                            <span
                              style={{
                                fontSize: '12px',
                                color: 'var(--text-muted)',
                                fontWeight: 'normal'
                              }}
                            >
                              {item.Unit}
                            </span>
                          </td>
                          <td style={{ color: 'var(--text-muted)', fontWeight: 600 }}>
                            Rs {item.CostPrice.toFixed(2)}
                          </td>
                          <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                            Rs {item.SellingPrice.toFixed(2)}
                          </td>
                          <td style={{ fontWeight: 900, textAlign: 'right', fontSize: '15px' }}>
                            Rs {(item.InitialQuantity * item.CostPrice).toFixed(2)}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className="pos-btn neutral" onClick={() => setViewingBillDetails(null)}>
                BACK TO PROFILE
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
