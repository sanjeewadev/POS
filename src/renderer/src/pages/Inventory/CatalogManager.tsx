// src/renderer/src/pages/Inventory/CatalogManager.tsx
import React, { useState, useEffect, useMemo } from 'react'
import Swal from 'sweetalert2'
import { Category, Product } from '../../types/models'
import styles from './CatalogManager.module.css'

export default function CatalogManager() {
  const [categories, setCategories] = useState<Category[]>([])
  const [products, setProducts] = useState<Product[]>([])

  // State: File Explorer
  const [selectedFolderId, setSelectedFolderId] = useState<number | null>(null)
  const [expandedFolders, setExpandedFolders] = useState<Set<number>>(new Set())
  const [globalSearch, setGlobalSearch] = useState('')

  // State: Creation Modals
  const [modalView, setModalView] = useState<'CLOSED' | 'CHOICE' | 'FOLDER' | 'PRODUCT'>('CLOSED')
  const [newItemName, setNewItemName] = useState('')
  const [newPrintName, setNewPrintName] = useState('')

  // 🚀 BARCODE STATE
  const [newBarcode, setNewBarcode] = useState('')

  const [prodUnit, setProdUnit] = useState('Pcs')
  const [newQuantityType, setNewQuantityType] = useState('quantity')

  // State: Viewing and Editing
  const [viewingProduct, setViewingProduct] = useState<Product | null>(null)
  const [productBatches, setProductBatches] = useState<any[]>([])

  const [editingProduct, setEditingProduct] = useState<Product | null>(null)
  const [editProdName, setEditProdName] = useState('')
  const [editPrintName, setEditPrintName] = useState('')
  const [editBarcode, setEditBarcode] = useState('') // 🚀 Allow editing barcodes
  const [editProdUnit, setEditProdUnit] = useState('')
  const [editQuantityType, setEditQuantityType] = useState('quantity')
  const [editProdFolder, setEditProdFolder] = useState<number>(0)

  const [editingFolder, setEditingFolder] = useState<Category | null>(null)
  const [editFolderName, setEditFolderName] = useState('')

  const loadData = async () => {
    try {
      // @ts-ignore
      setCategories(await window.api.getCategories())
      // @ts-ignore
      setProducts(await window.api.getProducts())
    } catch (err) {
      console.error('Data load failed', err)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  // --- ACTIONS: BACK & DELETE ---
  const handleBack = () => {
    if (!selectedFolderId) return
    const currentFolder = categories.find((c) => c.Id === selectedFolderId)
    if (currentFolder) setSelectedFolderId(currentFolder.ParentId)
  }

  const handleDeleteFolder = async (id: number) => {
    const hasSubFolders = categories.some((c) => c.ParentId === id)
    const hasProducts = products.some((p) => p.CategoryId === id)

    if (hasSubFolders || hasProducts) {
      return Swal.fire(
        'Action Denied',
        'This folder is not empty! Move or delete all products inside it first.',
        'error'
      )
    }

    const confirmResult = await Swal.fire({
      title: 'Delete Folder?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      confirmButtonText: 'Yes, delete'
    })

    if (confirmResult.isConfirmed) {
      try {
        // @ts-ignore
        await window.api.deleteCategory(id)
        if (selectedFolderId === id) setSelectedFolderId(null)
        loadData()
      } catch (err) {
        Swal.fire('Error', 'Error deleting folder.', 'error')
      }
    }
  }

  const handleDeleteProduct = async (id: number) => {
    const confirmResult = await Swal.fire({
      title: 'Delete Product?',
      text: 'Past sales history will be safely preserved.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#dc2626',
      confirmButtonText: 'Yes, delete product'
    })

    if (confirmResult.isConfirmed) {
      try {
        // @ts-ignore
        await window.api.softDeleteProduct(id)
        loadData()
      } catch (err) {
        Swal.fire('Error', 'Error deleting product.', 'error')
      }
    }
  }

  const handleViewProduct = async (product: Product) => {
    setViewingProduct(product)
    try {
      // @ts-ignore
      const batches = await window.api.getProductBatches(product.Id)
      setProductBatches(batches || [])
    } catch (err) {
      setProductBatches([])
    }
  }

  // --- 🚀 GENERATE RANDOM BARCODE ---
  const handleGenerateBarcode = () => {
    const randomCode = Math.floor(100000000000 + Math.random() * 900000000000).toString()
    setNewBarcode(randomCode)
  }

  const handleGenerateEditBarcode = () => {
    const randomCode = Math.floor(100000000000 + Math.random() * 900000000000).toString()
    setEditBarcode(randomCode)
  }

  // --- ACTIONS: CREATION ---
  const handleSaveFolder = async (e: React.FormEvent) => {
    e.preventDefault()
    const safeName = newItemName.trim()
    if (!safeName) return Swal.fire('Error', 'Folder name cannot be empty.', 'error')

    try {
      // @ts-ignore
      await window.api.addCategory({ Name: safeName, ParentId: selectedFolderId })
      setModalView('CLOSED')
      setNewItemName('')
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Failed to create folder.', 'error')
    }
  }

  const handleSaveProduct = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedFolderId)
      return Swal.fire(
        'Action Required',
        'You must be inside a folder to create a product.',
        'warning'
      )

    const safeName = newItemName.trim()
    if (!safeName) return Swal.fire('Error', 'Product name cannot be empty.', 'error')

    // 🚀 BARCODE LOGIC: If they left it blank, generate one automatically
    const finalBarcode =
      newBarcode.trim() || Math.floor(100000000000 + Math.random() * 900000000000).toString()

    const payload = {
      Name: safeName,
      PrintName: newPrintName.trim() || safeName.substring(0, 20),
      Barcode: finalBarcode,
      CategoryId: selectedFolderId,
      Unit: prodUnit,
      QuantityType: newQuantityType,
      BuyingPrice: 0,
      SellingPrice: 0,
      DiscountLimit: 0,
      Quantity: 0,
      IsActive: 1
    }

    try {
      // @ts-ignore
      await window.api.addProduct(payload)
      setModalView('CLOSED')
      setNewItemName('')
      setNewPrintName('')
      setNewBarcode('') // Reset barcode
      setProdUnit('Pcs')
      setNewQuantityType('quantity')
      loadData()
      Swal.fire({ title: 'Saved!', icon: 'success', timer: 1500, showConfirmButton: false })
    } catch (err: any) {
      if (err.message && err.message.includes('UNIQUE constraint failed')) {
        Swal.fire('Duplicate Barcode', 'A product with this barcode already exists!', 'error')
      } else {
        Swal.fire('Error', 'Failed to create product.', 'error')
      }
    }
  }

  // --- ACTIONS: EDITING ---
  const openEditFolder = (folder: Category) => {
    setEditingFolder(folder)
    setEditFolderName(folder.Name)
  }

  const handleUpdateFolder = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editingFolder) return
    const safeName = editFolderName.trim()
    if (!safeName) return Swal.fire('Error', 'Folder name cannot be empty.', 'error')

    try {
      // @ts-ignore
      await window.api.updateCategory({ ...editingFolder, Name: safeName })
      setEditingFolder(null)
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Error updating folder.', 'error')
    }
  }

  const openEditProduct = (product: Product) => {
    setEditingProduct(product)
    setEditProdName(product.Name)
    setEditPrintName(product.PrintName || '')
    setEditBarcode(product.Barcode || '')
    setEditProdUnit(product.Unit || 'Pcs')
    setEditQuantityType(product.QuantityType || 'quantity')
    setEditProdFolder(product.CategoryId)
  }

  const handleUpdateProduct = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!editingProduct) return
    const safeName = editProdName.trim()
    if (!safeName) return Swal.fire('Error', 'Product name cannot be empty.', 'error')

    try {
      const payload = {
        ...editingProduct,
        Name: safeName,
        PrintName: editPrintName.trim(),
        Barcode: editBarcode.trim() || editingProduct.Barcode, // Keep old if they accidentally cleared it
        Unit: editProdUnit,
        QuantityType: editQuantityType,
        CategoryId: editProdFolder
      }
      // @ts-ignore
      await window.api.updateProduct(payload)
      setEditingProduct(null)
      loadData()
      Swal.fire({ title: 'Updated!', icon: 'success', timer: 1500, showConfirmButton: false })
    } catch (err: any) {
      if (err.message && err.message.includes('UNIQUE constraint failed')) {
        Swal.fire('Duplicate Barcode', 'A product with this barcode already exists!', 'error')
      } else {
        Swal.fire('Error', 'Error updating product.', 'error')
      }
    }
  }

  // --- UI RENDERERS ---
  const treeContent = useMemo(() => {
    const renderTree = (parentId: number | null, depth: number = 0) => {
      const children = categories.filter((c) => c.ParentId === parentId)
      if (children.length === 0) return null

      return children.map((cat) => {
        const hasChildren = categories.some((c) => c.ParentId === cat.Id)
        const isExpanded = expandedFolders.has(cat.Id)
        const isActive = selectedFolderId === cat.Id

        return (
          <div key={cat.Id}>
            <div
              className={`${styles.treeNode} ${isActive ? styles.active : ''}`}
              style={{ paddingLeft: `${depth * 15 + 10}px` }}
              onClick={() => {
                setSelectedFolderId(cat.Id)
                setGlobalSearch('')
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
  }, [categories, expandedFolders, selectedFolderId])

  const displayedFolders = useMemo(() => {
    if (globalSearch) return []
    return categories.filter((c) => c.ParentId === selectedFolderId)
  }, [categories, selectedFolderId, globalSearch])

  const displayedProducts = useMemo(() => {
    if (globalSearch) {
      const q = globalSearch.toLowerCase()
      return products.filter(
        (p) =>
          p.Name.toLowerCase().includes(q) || (p.Barcode && p.Barcode.toLowerCase().includes(q))
      )
    }
    return selectedFolderId ? products.filter((p) => p.CategoryId === selectedFolderId) : []
  }, [products, selectedFolderId, globalSearch])

  const currentFolderName = globalSearch
    ? 'Search Results'
    : selectedFolderId
      ? categories.find((c) => c.Id === selectedFolderId)?.Name
      : 'Root Directory'

  return (
    <div className={styles.container}>
      {/* LAYER 1: FILE EXPLORER (LEFT) */}
      <div className={styles.leftPanel}>
        <div className={styles.panelHeader}>Explorer</div>
        <div className={styles.treeContainer}>{treeContent}</div>
      </div>

      {/* LAYER 2: CONTENTS (RIGHT) */}
      <div className={styles.rightPanel}>
        <div className={styles.rightHeader}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '15px' }}>
            {!globalSearch && selectedFolderId !== null && (
              <button
                className="pos-btn neutral"
                onClick={handleBack}
                style={{ padding: '0 15px', minHeight: '40px' }}
              >
                Back
              </button>
            )}
            <div className={styles.breadcrumb}>
              <span>Path: </span> {currentFolderName}
            </div>
          </div>

          <div style={{ display: 'flex', gap: '15px', alignItems: 'center' }}>
            <input
              type="text"
              className="pos-input"
              placeholder="Search catalog..."
              value={globalSearch}
              onChange={(e) => {
                setGlobalSearch(e.target.value)
                if (e.target.value) setSelectedFolderId(null)
              }}
              style={{ width: '300px', minHeight: '40px' }}
            />
            <button
              className="pos-btn success"
              onClick={() => setModalView('CHOICE')}
              disabled={!!globalSearch}
              style={{ minHeight: '40px' }}
            >
              + ADD NEW
            </button>
          </div>
        </div>

        <div className={styles.tableWrapper}>
          <table className={styles.classicTable}>
            <thead>
              <tr>
                <th>TYPE & NAME</th>
                <th>INFO</th>
                <th style={{ textAlign: 'right' }}>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {displayedFolders.map((folder) => (
                <tr key={`cat-${folder.Id}`}>
                  <td style={{ fontWeight: 700 }}>
                    <span className={styles.rowIcon}>📁</span> {folder.Name}
                  </td>
                  <td style={{ color: 'var(--text-muted)' }}>Folder</td>
                  <td style={{ display: 'flex', gap: '5px', justifyContent: 'flex-end' }}>
                    <button
                      className="pos-btn neutral"
                      onClick={() => setSelectedFolderId(folder.Id)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      OPEN
                    </button>
                    <button
                      className="pos-btn warning"
                      onClick={() => openEditFolder(folder)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      EDIT
                    </button>
                    <button
                      className="pos-btn danger"
                      onClick={() => handleDeleteFolder(folder.Id)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      DEL
                    </button>
                  </td>
                </tr>
              ))}

              {displayedProducts.map((prod) => (
                <tr key={`prod-${prod.Id}`}>
                  <td style={{ fontWeight: 700 }}>
                    <span className={styles.rowIcon}>📦</span> {prod.Name}
                  </td>
                  <td style={{ color: 'var(--text-muted)' }}>
                    SKU:{' '}
                    <span
                      style={{
                        fontFamily: 'monospace',
                        color: 'var(--brand-primary)',
                        fontWeight: 800
                      }}
                    >
                      {prod.Barcode}
                    </span>{' '}
                    | {prod.Unit}
                  </td>
                  <td style={{ display: 'flex', gap: '5px', justifyContent: 'flex-end' }}>
                    <button
                      className="pos-btn neutral"
                      onClick={() => handleViewProduct(prod)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      VIEW
                    </button>
                    <button
                      className="pos-btn warning"
                      onClick={() => openEditProduct(prod)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      EDIT
                    </button>
                    <button
                      className="pos-btn danger"
                      onClick={() => handleDeleteProduct(prod.Id)}
                      style={{ minHeight: '30px', padding: '5px 15px', fontSize: '11px' }}
                    >
                      DEL
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* ========================================================================= */}
      {/* MODAL 1: CREATION MACHINE */}
      {/* ========================================================================= */}
      {modalView !== 'CLOSED' && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px' }}>
                {modalView === 'CHOICE' && 'What do you want to create?'}
                {modalView === 'FOLDER' && 'Create Sub-Folder'}
                {modalView === 'PRODUCT' && 'Create Product'}
              </h2>
              <button
                className="pos-btn neutral"
                style={{ minHeight: '30px', padding: '5px 10px' }}
                onClick={() => setModalView('CLOSED')}
              >
                ✖
              </button>
            </div>

            {modalView === 'CHOICE' && (
              <div className={styles.modalBody}>
                <div style={{ display: 'flex', gap: '20px', justifyContent: 'center' }}>
                  <button
                    className="pos-btn neutral"
                    onClick={() => setModalView('FOLDER')}
                    style={{ padding: '30px', flex: 1, flexDirection: 'column' }}
                  >
                    <span style={{ fontSize: '32px' }}>📁</span> <br /> New Folder
                  </button>
                  <button
                    className="pos-btn success"
                    style={{ padding: '30px', flex: 1, flexDirection: 'column' }}
                    onClick={() => {
                      if (!selectedFolderId) {
                        Swal.fire(
                          'Action Required',
                          'You must OPEN a folder before creating a product!',
                          'warning'
                        )
                        return
                      }
                      setModalView('PRODUCT')
                    }}
                  >
                    <span style={{ fontSize: '32px' }}>📦</span> <br /> New Product
                  </button>
                </div>
              </div>
            )}

            {modalView === 'FOLDER' && (
              <form onSubmit={handleSaveFolder}>
                <div className={styles.modalBody}>
                  <div style={{ marginBottom: '15px' }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      FOLDER NAME
                    </label>
                    <input
                      type="text"
                      className="pos-input"
                      value={newItemName}
                      onChange={(e) => setNewItemName(e.target.value)}
                      required
                      autoFocus
                    />
                  </div>
                </div>
                <div className={styles.modalFooter}>
                  <button
                    type="button"
                    className="pos-btn neutral"
                    onClick={() => setModalView('CHOICE')}
                  >
                    Back
                  </button>
                  <button type="submit" className="pos-btn success">
                    Save Folder
                  </button>
                </div>
              </form>
            )}

            {modalView === 'PRODUCT' && (
              <form onSubmit={handleSaveProduct}>
                <div className={styles.modalBody}>
                  <div style={{ marginBottom: '15px' }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      PRODUCT NAME (FULL) *
                    </label>
                    <input
                      type="text"
                      className="pos-input"
                      value={newItemName}
                      onChange={(e) => setNewItemName(e.target.value)}
                      required
                      autoFocus
                      placeholder="e.g. Sunlight Soap 100g"
                    />
                  </div>

                  <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                    <div style={{ flex: 1 }}>
                      <label
                        style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                      >
                        BARCODE / SKU
                      </label>
                      <div style={{ display: 'flex', gap: '5px' }}>
                        {/* 🚀 If user scans a physical barcode, it types here. If they want a random one, they click the button */}
                        <input
                          type="text"
                          className="pos-input"
                          value={newBarcode}
                          onChange={(e) => setNewBarcode(e.target.value)}
                          placeholder="Scan or Generate..."
                        />
                        <button
                          type="button"
                          className="pos-btn neutral"
                          onClick={handleGenerateBarcode}
                          style={{ minHeight: '40px', padding: '0 10px' }}
                          title="Generate Random Barcode"
                        >
                          🎲
                        </button>
                      </div>
                    </div>
                    <div style={{ flex: 1 }}>
                      <label
                        style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                      >
                        RECEIPT PRINT NAME
                      </label>
                      <input
                        type="text"
                        className="pos-input"
                        value={newPrintName}
                        onChange={(e) => setNewPrintName(e.target.value)}
                        placeholder="Auto-generated if blank"
                        maxLength={20}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                    <div style={{ flex: 1 }}>
                      <label
                        style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                      >
                        BASE UNIT
                      </label>
                      <select
                        className="pos-input"
                        value={prodUnit}
                        onChange={(e) => setProdUnit(e.target.value)}
                      >
                        <option value="Pcs">Pcs</option>
                        <option value="Box">Box</option>
                        <option value="Kg">Kg</option>
                        <option value="m">m</option>
                      </select>
                    </div>
                    <div style={{ flex: 1 }}>
                      <label
                        style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                      >
                        SOLD BY (DECIMALS)
                      </label>
                      <select
                        className="pos-input"
                        value={newQuantityType}
                        onChange={(e) => setNewQuantityType(e.target.value)}
                      >
                        <option value="quantity">Whole Items Only (1, 2)</option>
                        <option value="kg">Weight/Length (0.5, 1.25)</option>
                      </select>
                    </div>
                  </div>
                </div>
                <div className={styles.modalFooter}>
                  <button
                    type="button"
                    className="pos-btn neutral"
                    onClick={() => setModalView('CHOICE')}
                  >
                    Back
                  </button>
                  <button type="submit" className="pos-btn success">
                    Save Product
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: EDIT FOLDER */}
      {/* ========================================================================= */}
      {editingFolder !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px' }}>Edit Folder</h2>
              <button
                className="pos-btn neutral"
                style={{ minHeight: '30px', padding: '5px 10px' }}
                onClick={() => setEditingFolder(null)}
              >
                ✖
              </button>
            </div>
            <form onSubmit={handleUpdateFolder}>
              <div className={styles.modalBody}>
                <label style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}>
                  FOLDER NAME
                </label>
                <input
                  type="text"
                  className="pos-input"
                  value={editFolderName}
                  onChange={(e) => setEditFolderName(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <div className={styles.modalFooter}>
                <button
                  type="button"
                  className="pos-btn neutral"
                  onClick={() => setEditingFolder(null)}
                >
                  Cancel
                </button>
                <button type="submit" className="pos-btn warning">
                  Update Folder
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 3: EDIT PRODUCT */}
      {/* ========================================================================= */}
      {editingProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBox}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px' }}>Edit Product</h2>
              <button
                className="pos-btn neutral"
                style={{ minHeight: '30px', padding: '5px 10px' }}
                onClick={() => setEditingProduct(null)}
              >
                ✖
              </button>
            </div>
            <form onSubmit={handleUpdateProduct}>
              <div className={styles.modalBody}>
                <div style={{ marginBottom: '15px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}>
                    PRODUCT NAME
                  </label>
                  <input
                    type="text"
                    className="pos-input"
                    value={editProdName}
                    onChange={(e) => setEditProdName(e.target.value)}
                    required
                  />
                </div>

                <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                  <div style={{ flex: 1 }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      BARCODE / SKU
                    </label>
                    <div style={{ display: 'flex', gap: '5px' }}>
                      <input
                        type="text"
                        className="pos-input"
                        value={editBarcode}
                        onChange={(e) => setEditBarcode(e.target.value)}
                        required
                      />
                      <button
                        type="button"
                        className="pos-btn neutral"
                        onClick={handleGenerateEditBarcode}
                        style={{ minHeight: '40px', padding: '0 10px' }}
                      >
                        🎲
                      </button>
                    </div>
                  </div>
                  <div style={{ flex: 1 }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      RECEIPT PRINT NAME
                    </label>
                    <input
                      type="text"
                      className="pos-input"
                      value={editPrintName}
                      onChange={(e) => setEditPrintName(e.target.value)}
                      maxLength={20}
                    />
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                  <div style={{ flex: 1 }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      BASE UNIT
                    </label>
                    <select
                      className="pos-input"
                      value={editProdUnit}
                      onChange={(e) => setEditProdUnit(e.target.value)}
                    >
                      <option value="Pcs">Pcs</option>
                      <option value="Box">Box</option>
                      <option value="Kg">Kg</option>
                      <option value="m">m</option>
                    </select>
                  </div>
                  <div style={{ flex: 1 }}>
                    <label
                      style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}
                    >
                      SOLD BY
                    </label>
                    <select
                      className="pos-input"
                      value={editQuantityType}
                      onChange={(e) => setEditQuantityType(e.target.value)}
                    >
                      <option value="quantity">Whole Items Only</option>
                      <option value="kg">Weight/Length (Decimals)</option>
                    </select>
                  </div>
                </div>
                <div style={{ marginBottom: '15px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 800, color: 'var(--text-muted)' }}>
                    MOVE TO FOLDER
                  </label>
                  <select
                    className="pos-input"
                    value={editProdFolder}
                    onChange={(e) => setEditProdFolder(Number(e.target.value))}
                  >
                    {categories.map((c) => (
                      <option key={c.Id} value={c.Id}>
                        {c.Name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className={styles.modalFooter}>
                <button
                  type="button"
                  className="pos-btn neutral"
                  onClick={() => setEditingProduct(null)}
                >
                  Cancel
                </button>
                <button type="submit" className="pos-btn warning">
                  Update Product
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 4: VIEW GRN BATCHES */}
      {/* ========================================================================= */}
      {viewingProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0, fontSize: '20px' }}>{viewingProduct.Name}</h2>
              <button
                className="pos-btn neutral"
                style={{ minHeight: '30px', padding: '5px 10px' }}
                onClick={() => setViewingProduct(null)}
              >
                ✖
              </button>
            </div>
            <div className={styles.modalBody}>
              <h3
                style={{
                  marginBottom: '15px',
                  fontSize: '14px',
                  textTransform: 'uppercase',
                  color: 'var(--text-muted)'
                }}
              >
                Active Inventory Batches
              </h3>
              {productBatches.length === 0 ? (
                <p style={{ color: 'var(--text-muted)' }}>
                  No active batches found. Receive GRN or Quick Add stock to see data here.
                </p>
              ) : (
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>Date Received</th>
                      <th>Original Qty</th>
                      <th>Current Qty</th>
                      <th>Cost Price</th>
                      <th>Selling Price</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productBatches.map((b, i) => (
                      <tr key={i}>
                        <td style={{ color: 'var(--text-muted)', fontWeight: 600 }}>
                          {new Date(b.ReceivedDate).toLocaleDateString()}
                        </td>
                        <td>{b.InitialQuantity}</td>
                        <td style={{ fontWeight: 900, color: 'var(--brand-primary)' }}>
                          {b.RemainingQuantity}
                        </td>
                        <td style={{ color: 'var(--text-muted)' }}>Rs {b.CostPrice.toFixed(2)}</td>
                        <td style={{ color: 'var(--action-success)', fontWeight: 800 }}>
                          Rs {b.SellingPrice.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
