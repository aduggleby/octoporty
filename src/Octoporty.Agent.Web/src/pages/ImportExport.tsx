// ═══════════════════════════════════════════════════════════════════════════
// IMPORT / EXPORT PAGE
// Backup and restore Agent definitions (JSON export/import) and SQLite backups
// ═══════════════════════════════════════════════════════════════════════════

import { useCallback, useMemo, useState } from 'react'
import { motion } from 'motion/react'
import { api } from '../api/client'
import { useToast } from '../hooks/useToast'
import type { AgentDefinitionsImportV1, AgentDefinitionsImportResponse } from '../types'

function downloadBlob(blob: Blob, filename: string) {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  window.URL.revokeObjectURL(url)
}

export function ImportExportPage() {
  const { addToast } = useToast()
  const [isDownloadingDb, setIsDownloadingDb] = useState(false)
  const [isExportingJson, setIsExportingJson] = useState(false)
  const [isImportingJson, setIsImportingJson] = useState(false)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [lastImportResult, setLastImportResult] = useState<AgentDefinitionsImportResponse | null>(null)

  const canImport = useMemo(() => selectedFile !== null && !isImportingJson, [selectedFile, isImportingJson])

  const handleDownloadSqlite = useCallback(async () => {
    setIsDownloadingDb(true)
    try {
      const { blob, filename } = await api.downloadSqliteBackup()
      downloadBlob(blob, filename)
      addToast('success', 'SQLite backup downloaded', filename)
    } catch (err) {
      const error = err as Error
      addToast('error', 'Failed to download SQLite backup', error.message)
    } finally {
      setIsDownloadingDb(false)
    }
  }, [addToast])

  const handleExportJson = useCallback(async () => {
    setIsExportingJson(true)
    try {
      const data = await api.exportDefinitions()
      const json = JSON.stringify(data, null, 2)
      const blob = new Blob([json], { type: 'application/json' })
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-')
      const filename = `octoporty-agent-definitions-${timestamp}.json`
      downloadBlob(blob, filename)
      addToast('success', 'Definitions exported', filename)
    } catch (err) {
      const error = err as Error
      addToast('error', 'Failed to export definitions', error.message)
    } finally {
      setIsExportingJson(false)
    }
  }, [addToast])

  const handleImportJson = useCallback(async () => {
    if (!selectedFile) return

    setIsImportingJson(true)
    setLastImportResult(null)
    try {
      const text = await selectedFile.text()
      const payload = JSON.parse(text) as AgentDefinitionsImportV1
      const result = await api.importDefinitions(payload)
      setLastImportResult(result)

      if (result.success) {
        addToast('success', 'Import completed', `Created ${result.created}, updated ${result.updated}, skipped ${result.skipped}`)
      } else {
        addToast('error', 'Import failed', result.error ?? 'Unknown error')
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Invalid JSON file'
      addToast('error', 'Failed to import definitions', message)
    } finally {
      setIsImportingJson(false)
    }
  }, [addToast, selectedFile])

  return (
    <div>
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8">
        <div>
          <h1 className="page-title">Import/Export</h1>
          <p className="page-subtitle">Backup and restore your Agent mappings and settings</p>
        </div>
      </div>

      <div className="mb-6 p-4 bg-surface-1 rounded-lg border border-border-subtle text-sm text-text-secondary">
        <div className="flex items-start gap-3">
          <svg className="w-5 h-5 text-amber-400 mt-0.5" viewBox="0 0 20 20" fill="currentColor">
            <path
              fillRule="evenodd"
              d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
              clipRule="evenodd"
            />
          </svg>
          <div>
            <div className="font-semibold text-text-primary mb-1">Import behavior</div>
            <div>
              Imports are merge-only: mappings are upserted by <span className="font-mono text-text-primary">ExternalDomain</span>. Missing mappings are not deleted.
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="panel"
        >
          <div className="panel-header">
            <svg className="w-4 h-4 text-cyan-base" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 3v12" />
              <path d="M8 11l4 4 4-4" />
              <path d="M20 21H4" />
            </svg>
            <span className="panel-title">Export</span>
          </div>
          <div className="panel-body space-y-3">
            <button
              onClick={handleExportJson}
              disabled={isExportingJson}
              className="btn btn-primary w-full"
            >
              {isExportingJson ? 'Exporting…' : 'Download Definitions (JSON)'}
            </button>

            <button
              onClick={handleDownloadSqlite}
              disabled={isDownloadingDb}
              className="btn btn-ghost w-full"
            >
              {isDownloadingDb ? 'Preparing…' : 'Download SQLite Backup'}
            </button>

            <div className="text-xs text-text-muted">
              JSON export is portable and safe for migration. SQLite backup is a full database snapshot.
            </div>
          </div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="panel"
        >
          <div className="panel-header">
            <svg className="w-4 h-4 text-emerald-base" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 21V9" />
              <path d="M8 13l4-4 4 4" />
              <path d="M20 3H4" />
            </svg>
            <span className="panel-title">Import</span>
          </div>
          <div className="panel-body space-y-3">
            <input
              type="file"
              accept="application/json,.json"
              onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)}
              className="block w-full text-sm text-text-secondary file:mr-3 file:py-2 file:px-3 file:rounded-md file:border file:border-border-subtle file:bg-surface-1 file:text-text-primary hover:file:bg-surface-2"
            />

            <button
              onClick={handleImportJson}
              disabled={!canImport}
              className="btn btn-primary w-full"
            >
              {isImportingJson ? 'Importing…' : 'Import Definitions (JSON)'}
            </button>

            {lastImportResult && (
              <div className="p-3 rounded-lg border border-border-subtle bg-surface-1 text-sm">
                <div className="font-semibold text-text-primary mb-1">
                  {lastImportResult.success ? 'Import Summary' : 'Import Failed'}
                </div>
                {!lastImportResult.success && lastImportResult.error && (
                  <div className="text-rose-base mb-2">{lastImportResult.error}</div>
                )}
                <div className="text-text-secondary">
                  Created: <span className="font-mono text-text-primary">{lastImportResult.created}</span>{' '}
                  Updated: <span className="font-mono text-text-primary">{lastImportResult.updated}</span>{' '}
                  Skipped: <span className="font-mono text-text-primary">{lastImportResult.skipped}</span>
                </div>
                {lastImportResult.errors?.length > 0 && (
                  <div className="mt-2 text-xs text-text-muted whitespace-pre-wrap">
                    {lastImportResult.errors.join('\n')}
                  </div>
                )}
              </div>
            )}
          </div>
        </motion.div>
      </div>
    </div>
  )
}

