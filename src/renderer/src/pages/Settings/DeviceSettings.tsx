// src/renderer/src/pages/Settings/DeviceSettings.tsx
import { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import Swal from 'sweetalert2'
import { RiTabletLine, RiKeyboardLine, RiCalculatorLine } from 'react-icons/ri'
import styles from './DeviceSettings.module.css'

export default function DeviceSettings() {
  const { posNumpadEnabled, globalNumpadEnabled, togglePosNumpad, toggleGlobalNumpad } = useAuth()
  const [isProcessing, setIsProcessing] = useState(false)

  // Local state for smooth UI toggling before database confirms
  const [localPosEnabled, setLocalPosEnabled] = useState(posNumpadEnabled)
  const [localGlobalEnabled, setLocalGlobalEnabled] = useState(globalNumpadEnabled)

  useEffect(() => {
    setLocalPosEnabled(posNumpadEnabled)
    setLocalGlobalEnabled(globalNumpadEnabled)
  }, [posNumpadEnabled, globalNumpadEnabled])

  const handlePosToggle = async () => {
    setIsProcessing(true)
    const newValue = !localPosEnabled
    setLocalPosEnabled(newValue) // Instant UI feedback

    try {
      await togglePosNumpad(newValue)
      Swal.fire({
        title: newValue ? 'Numpad Enabled' : 'Numpad Disabled',
        text: `POS Terminal will now use the ${newValue ? 'Custom Touch Numpad' : 'Native OS Keyboard'}.`,
        icon: 'success',
        timer: 1500,
        showConfirmButton: false,
        toast: true,
        position: 'bottom-end'
      })
    } catch (error) {
      setLocalPosEnabled(!newValue) // Revert on failure
      Swal.fire('Error', 'Failed to update setting', 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  const handleGlobalToggle = async () => {
    setIsProcessing(true)
    const newValue = !localGlobalEnabled
    setLocalGlobalEnabled(newValue) // Instant UI feedback

    try {
      await toggleGlobalNumpad(newValue)
      Swal.fire({
        title: newValue ? 'Global Numpad Enabled' : 'Global Numpad Disabled',
        text: `Management screens will now use the ${newValue ? 'Custom Touch Numpad' : 'Native OS Keyboard'}.`,
        icon: 'success',
        timer: 1500,
        showConfirmButton: false,
        toast: true,
        position: 'bottom-end'
      })
    } catch (error) {
      setLocalGlobalEnabled(!newValue) // Revert on failure
      Swal.fire('Error', 'Failed to update setting', 'error')
    } finally {
      setIsProcessing(false)
    }
  }

  return (
    <div className={styles.container}>
      <div className={styles.mainPanel}>
        <div className={styles.panelHeader}>
          <h2 className="pos-page-title">Device & Interface Configuration</h2>
        </div>

        <div className={styles.panelBody}>
          <div className={styles.settingsGrid}>
            {/* Setting 1: POS Terminal Numpad */}
            <div className={styles.settingCard}>
              <div className={styles.cardHeader}>
                <RiTabletLine className={styles.headerIcon} />
                <div className={styles.headerText}>
                  <h3>POS Terminal Numpad</h3>
                  <p>
                    Overrides the native OS keyboard with a giant touch-friendly calculator layout
                    on the main Checkout screen.
                  </p>
                </div>
              </div>
              <div className={styles.cardBody}>
                <div className={styles.toggleWrapper}>
                  <div className={styles.statusBox}>
                    {localPosEnabled ? (
                      <span className={styles.statusActive}>
                        <RiCalculatorLine /> CUSTOM NUMPAD ACTIVE
                      </span>
                    ) : (
                      <span className={styles.statusInactive}>
                        <RiKeyboardLine /> NATIVE KEYBOARD ACTIVE
                      </span>
                    )}
                  </div>

                  <label className={styles.switch}>
                    <input
                      type="checkbox"
                      checked={localPosEnabled}
                      onChange={handlePosToggle}
                      disabled={isProcessing}
                    />
                    <span className={styles.slider}></span>
                  </label>
                </div>
              </div>
            </div>

            {/* Setting 2: Global Management Numpad */}
            <div className={styles.settingCard}>
              <div className={styles.cardHeader}>
                <RiCalculatorLine className={styles.headerIcon} />
                <div className={styles.headerText}>
                  <h3>Global Management Numpad</h3>
                  <p>
                    Overrides the native OS keyboard for number inputs in Inventory, Adjustments,
                    and Settings screens.
                  </p>
                </div>
              </div>
              <div className={styles.cardBody}>
                <div className={styles.toggleWrapper}>
                  <div className={styles.statusBox}>
                    {localGlobalEnabled ? (
                      <span className={styles.statusActive}>
                        <RiCalculatorLine /> CUSTOM NUMPAD ACTIVE
                      </span>
                    ) : (
                      <span className={styles.statusInactive}>
                        <RiKeyboardLine /> NATIVE KEYBOARD ACTIVE
                      </span>
                    )}
                  </div>

                  <label className={styles.switch}>
                    <input
                      type="checkbox"
                      checked={localGlobalEnabled}
                      onChange={handleGlobalToggle}
                      disabled={isProcessing}
                    />
                    <span className={styles.slider}></span>
                  </label>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
