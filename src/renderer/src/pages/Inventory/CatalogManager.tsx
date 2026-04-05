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

  // Global Search
  const [globalSearch, setGlobalSearch] = useState('')

  // State: Creation Modals
  const [modalView, setModalView] = useState<'CLOSED' | 'CHOICE' | 'FOLDER' | 'PRODUCT'>('CLOSED')
  const [newItemName, setNewItemName] = useState('')
  // 🚀 NEW: Fields for our enterprise database features
  const [newPrintName, setNewPrintName] = useState('')
  const [prodUnit, setProdUnit] = useState('Pcs')
  const [newQuantityType, setNewQuantityType] = useState('quantity')

  // State: Viewing and Editing
  const [viewingProduct, setViewingProduct] = useState<Product | null>(null)
  const [productBatches, setProductBatches] = useState<any[]>([])

  const [editingProduct, setEditingProduct] = useState<Product | null>(null)
  const [editProdName, setEditProdName] = useState('')
  const [editPrintName, setEditPrintName] = useState('')
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
      Swal.fire(
        'Action Denied',
        'This folder is not empty!\n\nYou must move or delete all products and sub-folders inside it before deleting. This protects your database history.',
        'error'
      )
      return
    }

    const confirmResult = await Swal.fire({
      title: 'Are you sure?',
      text: 'Do you want to delete this empty folder?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Yes, delete it!'
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
      text: 'Are you sure you want to remove this product from the catalog?\n\n(Note: Past sales history for this item will be safely preserved in the Recycle Bin.)',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Yes, delete product'
    })

    if (confirmResult.isConfirmed) {
      try {
        // @ts-ignore
        await window.api.softDeleteProduct(id) // 🚀 Changed to our new Soft Delete!
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

  // --- ACTIONS: CREATION ---
  const handleSaveFolder = async (e: React.FormEvent) => {
    e.preventDefault()

    const safeName = newItemName.trim()
    if (!safeName) {
      Swal.fire('Error', 'Folder name cannot be empty or just spaces.', 'error')
      return
    }

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
    if (!selectedFolderId) {
      Swal.fire('Action Required', 'You must be inside a folder to create a product.', 'warning')
      return
    }

    const safeName = newItemName.trim()
    if (!safeName) {
      Swal.fire('Error', 'Product name cannot be empty.', 'error')
      return
    }

    const generatedSKU = 'SKU-' + Math.floor(10000000 + Math.random() * 90000000)
    const payload = {
      Name: safeName,
      PrintName: newPrintName.trim() || safeName.substring(0, 20), // Auto-fallback if empty
      Barcode: generatedSKU,
      CategoryId: selectedFolderId,
      Unit: prodUnit,
      QuantityType: newQuantityType, // 🚀 Added to payload
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
      setProdUnit('Pcs')
      setNewQuantityType('quantity')
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Failed to create product.', 'error')
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
        Unit: editProdUnit,
        QuantityType: editQuantityType,
        CategoryId: editProdFolder
      }
      // @ts-ignore
      await window.api.updateProduct(payload)
      setEditingProduct(null)
      loadData()
    } catch (err: any) {
      Swal.fire('Error', 'Error updating product.', 'error')
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

  const getCatName = (id: number | null) => categories.find((c) => c.Id === id)?.Name || 'N/A'

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
              <button className="pos-btn neutral" onClick={handleBack} style={{ padding: '10px' }}>
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
              style={{ width: '300px' }}
            />
            <button
              className="pos-btn success"
              onClick={() => setModalView('CHOICE')}
              disabled={!!globalSearch}
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
                <th>ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              {displayedFolders.map((folder) => (
                <tr key={`cat-${folder.Id}`}>
                  <td style={{ fontWeight: 700 }}>
                    <span className={styles.rowIcon}>📁</span> {folder.Name}
                  </td>
                  <td style={{ color: 'var(--text-muted)' }}>Folder</td>
                  <td style={{ display: 'flex', gap: '5px' }}>
                    <button
                      className="pos-btn neutral"
                      onClick={() => setSelectedFolderId(folder.Id)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                    >
                      OPEN
                    </button>
                    <button
                      className="pos-btn warning"
                      onClick={() => openEditFolder(folder)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                    >
                      EDIT
                    </button>
                    <button
                      className="pos-btn danger"
                      onClick={() => handleDeleteFolder(folder.Id)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                    >
                      DEL
                    </button>
                  </td>
                </tr>
              ))}

              {displayedProducts.map((prod) => (
                <tr key={`prod-${prod.Id}`}>
                  <td style={{ fontWeight: 500 }}>
                    <span className={styles.rowIcon}>📦</span> {prod.Name}
                  </td>
                  <td style={{ color: 'var(--text-muted)' }}>
                    SKU: {prod.Barcode} | {prod.Unit} |{' '}
                    {prod.QuantityType === 'kg' ? '⚖️ Weight' : '🔢 Whole Items'}
                  </td>
                  <td style={{ display: 'flex', gap: '5px' }}>
                    <button
                      className="pos-btn neutral"
                      onClick={() => handleViewProduct(prod)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                    >
                      VIEW
                    </button>
                    <button
                      className="pos-btn warning"
                      onClick={() => openEditProduct(prod)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
                    >
                      EDIT
                    </button>
                    <button
                      className="pos-btn danger"
                      onClick={() => handleDeleteProduct(prod.Id)}
                      style={{ minHeight: '40px', padding: '5px 15px', fontSize: '12px' }}
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
              <button className={styles.iconCloseBtn} onClick={() => setModalView('CLOSED')}>
                ✖
              </button>
            </div>

            {modalView === 'CHOICE' && (
              <div className={styles.modalBody}>
                <div style={{ display: 'flex', gap: '20px', justifyContent: 'center' }}>
                  <button
                    className="pos-btn neutral"
                    onClick={() => setModalView('FOLDER')}
                    style={{ padding: '30px' }}
                  >
                    📁 New Folder
                  </button>
                  <button
                    className="pos-btn success"
                    style={{ padding: '30px' }}
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
                    📦 New Product
                  </button>
                </div>
              </div>
            )}

            {modalView === 'FOLDER' && (
              <form onSubmit={handleSaveFolder}>
                <div className={styles.modalBody}>
                  <div style={{ marginBottom: '15px' }}>
                    <label>Folder Name</label>
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
                    <label>Product Name (Full)</label>
                    <input
                      type="text"
                      className="pos-input"
                      value={newItemName}
                      onChange={(e) => setNewItemName(e.target.value)}
                      required
                      autoFocus
                      placeholder="Stanley Hammer 12oz"
                    />
                  </div>
                  <div style={{ marginBottom: '15px' }}>
                    <label>Receipt Name (Short Text)</label>
                    <input
                      type="text"
                      className="pos-input"
                      value={newPrintName}
                      onChange={(e) => setNewPrintName(e.target.value)}
                      placeholder="Leave blank to auto-generate"
                      maxLength={20}
                    />
                  </div>
                  <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                    <div style={{ flex: 1 }}>
                      <label>Base Unit</label>
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
                      <label>Sold By (Decimals)</label>
                      <select
                        className="pos-input"
                        value={newQuantityType}
                        onChange={(e) => setNewQuantityType(e.target.value)}
                      >
                        <option value="quantity">Whole Items Only (1, 2, 3)</option>
                        <option value="kg">Weight/Length (1.25, 0.5)</option>
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
              <button className={styles.iconCloseBtn} onClick={() => setEditingFolder(null)}>
                ✖
              </button>
            </div>
            <form onSubmit={handleUpdateFolder}>
              <div className={styles.modalBody}>
                <label>Folder Name</label>
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
              <button className={styles.iconCloseBtn} onClick={() => setEditingProduct(null)}>
                ✖
              </button>
            </div>
            <form onSubmit={handleUpdateProduct}>
              <div className={styles.modalBody}>
                <div style={{ marginBottom: '15px' }}>
                  <label>Product Name</label>
                  <input
                    type="text"
                    className="pos-input"
                    value={editProdName}
                    onChange={(e) => setEditProdName(e.target.value)}
                    required
                  />
                </div>
                <div style={{ marginBottom: '15px' }}>
                  <label>Receipt Name (Max 20 Chars)</label>
                  <input
                    type="text"
                    className="pos-input"
                    value={editPrintName}
                    onChange={(e) => setEditPrintName(e.target.value)}
                    maxLength={20}
                  />
                </div>
                <div style={{ display: 'flex', gap: '15px', marginBottom: '15px' }}>
                  <div style={{ flex: 1 }}>
                    <label>Base Unit</label>
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
                    <label>Sold By</label>
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
                  <label>Move to Folder</label>
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
      {/* MODAL 4: VIEW GRN BATCHES (Same as before) */}
      {/* ========================================================================= */}
      {viewingProduct !== null && (
        <div className={styles.modalOverlay}>
          <div className={styles.modalBoxView}>
            {/* View Product Modal Header & Body here... */}
            <div className={styles.modalHeader}>
              <h2 style={{ margin: 0 }}>{viewingProduct.Name}</h2>
              <button className="pos-btn neutral" onClick={() => setViewingProduct(null)}>
                Close
              </button>
            </div>
            <div className={styles.modalBody}>
              <h3 style={{ marginBottom: '15px' }}>Active Inventory Batches</h3>
              {productBatches.length === 0 ? (
                <p>No active batches found.</p>
              ) : (
                <table className={styles.classicTable}>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Original Qty</th>
                      <th>Current Qty</th>
                      <th>Selling Price</th>
                    </tr>
                  </thead>
                  <tbody>
                    {productBatches.map((b, i) => (
                      <tr key={i}>
                        <td>{new Date(b.ReceivedDate).toLocaleDateString()}</td>
                        <td>{b.InitialQuantity}</td>
                        <td style={{ fontWeight: 'bold' }}>{b.RemainingQuantity}</td>
                        <td>Rs {b.SellingPrice.toFixed(2)}</td>
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
