// src/renderer/src/components/Calculator/CalculatorModal.tsx
import React, { useState } from 'react'
import { RiCloseLine, RiDeleteBack2Line } from 'react-icons/ri'
import styles from './CalculatorModal.module.css'

interface CalculatorModalProps {
  onClose: () => void
}

export default function CalculatorModal({ onClose }: CalculatorModalProps) {
  const [displayValue, setDisplayValue] = useState('0')
  const [previousValue, setPreviousValue] = useState<number | null>(null)
  const [operator, setOperator] = useState<string | null>(null)
  const [waitingForOperand, setWaitingForOperand] = useState(false)
  const [equationString, setEquationString] = useState('')

  const inputDigit = (digit: string) => {
    if (waitingForOperand) {
      setDisplayValue(digit)
      setWaitingForOperand(false)
    } else {
      setDisplayValue(displayValue === '0' ? digit : displayValue + digit)
    }
  }

  const inputDot = () => {
    if (waitingForOperand) {
      setDisplayValue('0.')
      setWaitingForOperand(false)
      return
    }
    if (!displayValue.includes('.')) {
      setDisplayValue(displayValue + '.')
    }
  }

  const clearAll = () => {
    setDisplayValue('0')
    setPreviousValue(null)
    setOperator(null)
    setWaitingForOperand(false)
    setEquationString('')
  }

  const handleBackspace = () => {
    if (waitingForOperand) return
    setDisplayValue(displayValue.length > 1 ? displayValue.slice(0, -1) : '0')
  }

  const performOperation = (nextOperator: string) => {
    const inputValue = parseFloat(displayValue)

    if (previousValue == null) {
      setPreviousValue(inputValue)
      setEquationString(`${inputValue} ${nextOperator}`)
    } else if (operator) {
      const currentValue = previousValue || 0
      let newValue = 0

      switch (operator) {
        case '+':
          newValue = currentValue + inputValue
          break
        case '-':
          newValue = currentValue - inputValue
          break
        case '*':
          newValue = currentValue * inputValue
          break
        case '/':
          newValue = currentValue / inputValue
          break
        case '=':
          newValue = inputValue
          break
      }

      setPreviousValue(newValue)
      setDisplayValue(String(Number(newValue.toFixed(6)))) // Handles JS float math weirdness

      if (nextOperator === '=') {
        setEquationString('')
        setOperator(null)
        setWaitingForOperand(true)
        return
      } else {
        setEquationString(`${Number(newValue.toFixed(6))} ${nextOperator}`)
      }
    }

    setWaitingForOperand(true)
    setOperator(nextOperator)
  }

  return (
    <div className={styles.modalOverlay}>
      <div className={styles.calculatorContainer}>
        {/* Header */}
        <div className={styles.header}>
          <span className={styles.title}>Quick Calculator</span>
          <button className={styles.closeBtn} onClick={onClose}>
            <RiCloseLine size={24} />
          </button>
        </div>

        {/* Display Area */}
        <div className={styles.displayArea}>
          <div className={styles.equationText}>{equationString}</div>
          <div className={styles.mainDisplay}>{displayValue}</div>
        </div>

        {/* Keypad */}
        <div className={styles.keypad}>
          {/* Row 1 */}
          <button className={`${styles.btn} ${styles.btnDanger}`} onClick={clearAll}>
            C
          </button>
          <button
            className={`${styles.btn} ${styles.btnAction}`}
            onClick={() => performOperation('/')}
          >
            ÷
          </button>
          <button
            className={`${styles.btn} ${styles.btnAction}`}
            onClick={() => performOperation('*')}
          >
            ×
          </button>
          <button className={`${styles.btn} ${styles.btnWarning}`} onClick={handleBackspace}>
            <RiDeleteBack2Line size={24} />
          </button>

          {/* Row 2 */}
          <button className={styles.btn} onClick={() => inputDigit('7')}>
            7
          </button>
          <button className={styles.btn} onClick={() => inputDigit('8')}>
            8
          </button>
          <button className={styles.btn} onClick={() => inputDigit('9')}>
            9
          </button>
          <button
            className={`${styles.btn} ${styles.btnAction}`}
            onClick={() => performOperation('-')}
          >
            −
          </button>

          {/* Row 3 */}
          <button className={styles.btn} onClick={() => inputDigit('4')}>
            4
          </button>
          <button className={styles.btn} onClick={() => inputDigit('5')}>
            5
          </button>
          <button className={styles.btn} onClick={() => inputDigit('6')}>
            6
          </button>
          <button
            className={`${styles.btn} ${styles.btnAction}`}
            onClick={() => performOperation('+')}
          >
            +
          </button>

          {/* Row 4 & 5 Mixed for layout */}
          <div className={styles.bottomSection}>
            <div className={styles.numbersGrid}>
              <button className={styles.btn} onClick={() => inputDigit('1')}>
                1
              </button>
              <button className={styles.btn} onClick={() => inputDigit('2')}>
                2
              </button>
              <button className={styles.btn} onClick={() => inputDigit('3')}>
                3
              </button>
              <button className={`${styles.btn} ${styles.btnZero}`} onClick={() => inputDigit('0')}>
                0
              </button>
              <button className={styles.btn} onClick={inputDot}>
                .
              </button>
            </div>
            <button
              className={`${styles.btn} ${styles.btnEquals}`}
              onClick={() => performOperation('=')}
            >
              =
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
