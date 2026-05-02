// src/renderer/src/components/TouchNumberInput/TouchNumberInput.tsx
import React, { useState, useEffect } from 'react'
import { useAuth } from '../../store/AuthContext'
import { RiDeleteBack2Line, RiCheckLine, RiCloseLine } from 'react-icons/ri'
import styles from './TouchNumberInput.module.css'

interface TouchNumberInputProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  'onChange'
> {
  value: string | number
  onChange: (val: string) => void
  isPosScreen?: boolean // 🚀 Tells the component which global setting to check
}

export default function TouchNumberInput({
  value,
  onChange,
  isPosScreen = false,
  className,
  ...props
}: TouchNumberInputProps) {
  const { posNumpadEnabled, globalNumpadEnabled } = useAuth()
  const [isOpen, setIsOpen] = useState(false)
  const [tempValue, setTempValue] = useState(String(value))

  // Determine if we should use the Numpad based on the screen context
  const useNumpad = isPosScreen ? posNumpadEnabled : globalNumpadEnabled

  useEffect(() => {
    if (isOpen) setTempValue(String(value))
  }, [isOpen, value])

  const handleInputInteraction = (
    e: React.MouseEvent<HTMLInputElement> | React.FocusEvent<HTMLInputElement>
  ) => {
    if (useNumpad) {
      e.preventDefault()
      e.currentTarget.blur() // 🚀 Kills the native OS keyboard instantly
      setIsOpen(true)
    }
  }

  const handleKeyClick = (key: string) => {
    if (key === '.' && tempValue.includes('.')) return // Prevent double decimals

    setTempValue((prev) => {
      if (prev === '0' && key !== '.') return key
      return prev + key
    })
  }

  const handleBackspace = () => {
    setTempValue((prev) => (prev.length > 1 ? prev.slice(0, -1) : '0'))
  }

  const handleClear = () => {
    setTempValue('0')
  }

  const handleConfirm = () => {
    // Trim hanging decimals before sending
    const finalValue = tempValue.endsWith('.') ? tempValue.slice(0, -1) : tempValue
    onChange(finalValue)
    setIsOpen(false)
  }

  return (
    <>
      {/* THE ACTUAL INPUT VISIBLE ON SCREEN */}
      <input
        {...props}
        value={value}
        className={className}
        onChange={(e) => {
          if (!useNumpad) onChange(e.target.value) // Normal typing if Numpad is OFF
        }}
        onClick={handleInputInteraction}
        onFocus={handleInputInteraction}
        inputMode={useNumpad ? 'none' : 'decimal'} // HTML5 trick to suppress mobile keyboards
      />

      {/* THE TOUCH NUMPAD OVERLAY (Only renders when clicked) */}
      {isOpen && (
        <div className={styles.modalOverlay}>
          <div className={styles.numpadContainer}>
            <div className={styles.header}>
              <span className={styles.title}>Enter Value</span>
              <button type="button" className={styles.closeBtn} onClick={() => setIsOpen(false)}>
                <RiCloseLine size={24} />
              </button>
            </div>

            <div className={styles.displayArea}>{tempValue || '0'}</div>

            <div className={styles.numpadGrid}>
              {['7', '8', '9', '4', '5', '6', '1', '2', '3', 'C', '0', '.'].map((key) => (
                <button
                  key={key}
                  type="button"
                  className={styles.numBtn}
                  onClick={() => {
                    if (key === 'C') handleClear()
                    else handleKeyClick(key)
                  }}
                >
                  {key}
                </button>
              ))}
            </div>

            <div className={styles.actionRow}>
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.backspaceBtn}`}
                onClick={handleBackspace}
              >
                <RiDeleteBack2Line size={28} />
              </button>
              <button
                type="button"
                className={`${styles.actionBtn} ${styles.confirmBtn}`}
                onClick={handleConfirm}
              >
                <RiCheckLine size={28} /> DONE
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
