// ═══════════════════════════════════════════════════════════════════════════
// SIGNALR HOOK - REAL-TIME STATUS UPDATES
// ═══════════════════════════════════════════════════════════════════════════

import { useEffect, useRef, useState, useCallback } from 'react'
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import type { StatusUpdate, MappingStatusUpdate, GatewayLog } from '../types'

interface UseSignalROptions {
  onStatusUpdate?: (update: StatusUpdate) => void
  onMappingUpdate?: (update: MappingStatusUpdate) => void
  onGatewayLog?: (log: GatewayLog) => void
  onReconnecting?: () => void
  onReconnected?: () => void
  onDisconnected?: (error?: Error) => void
}

interface UseSignalRReturn {
  connectionState: HubConnectionState
  isConnected: boolean
}

export function useSignalR(options: UseSignalROptions = {}): UseSignalRReturn {
  const connectionRef = useRef<HubConnection | null>(null)
  const [connectionState, setConnectionState] = useState<HubConnectionState>(
    HubConnectionState.Disconnected
  )

  const token = localStorage.getItem('octoporty_token')

  const connect = useCallback(async () => {
    if (!token) return

    // Clean up existing connection
    if (connectionRef.current) {
      await connectionRef.current.stop()
    }

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/status', {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0, 2, 4, 8, 16, 30 (max) seconds
          const delay = Math.min(
            Math.pow(2, retryContext.previousRetryCount) * 1000,
            30000
          )
          return delay
        },
      })
      .configureLogging(LogLevel.Warning)
      .build()

    // Register event handlers
    connection.on('StatusUpdate', (update: StatusUpdate) => {
      options.onStatusUpdate?.(update)
    })

    connection.on('MappingStatusUpdate', (update: MappingStatusUpdate) => {
      options.onMappingUpdate?.(update)
    })

    connection.on('GatewayLog', (log: GatewayLog) => {
      options.onGatewayLog?.(log)
    })

    connection.onreconnecting((error) => {
      setConnectionState(HubConnectionState.Reconnecting)
      options.onReconnecting?.()
      console.log('SignalR reconnecting...', error)
    })

    connection.onreconnected((connectionId) => {
      setConnectionState(HubConnectionState.Connected)
      options.onReconnected?.()
      console.log('SignalR reconnected:', connectionId)
    })

    connection.onclose((error) => {
      setConnectionState(HubConnectionState.Disconnected)
      options.onDisconnected?.(error)
      console.log('SignalR disconnected:', error)
    })

    connectionRef.current = connection

    try {
      await connection.start()
      setConnectionState(HubConnectionState.Connected)
      console.log('SignalR connected')
    } catch (error) {
      setConnectionState(HubConnectionState.Disconnected)
      console.error('SignalR connection error:', error)
      // Retry connection after 5 seconds
      setTimeout(() => connect(), 5000)
    }
  }, [token, options])

  useEffect(() => {
    connect()

    return () => {
      connectionRef.current?.stop()
    }
  }, [connect])

  return {
    connectionState,
    isConnected: connectionState === HubConnectionState.Connected,
  }
}
