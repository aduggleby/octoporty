// ═══════════════════════════════════════════════════════════════════════════
// SIGNALR HOOK - REAL-TIME STATUS UPDATES
// ═══════════════════════════════════════════════════════════════════════════
// Uses a singleton connection pattern to prevent multiple connections.
// Callbacks are stored in refs to avoid dependency issues causing reconnects.

import { useEffect, useRef, useState } from 'react'
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

// Singleton connection state - shared across all hook instances
let sharedConnection: HubConnection | null = null
let connectionPromise: Promise<void> | null = null
let subscriberCount = 0

// Event subscribers - allows multiple components to receive updates
type StatusCallback = (update: StatusUpdate) => void
type MappingCallback = (update: MappingStatusUpdate) => void
type GatewayLogCallback = (log: GatewayLog) => void

const statusSubscribers = new Set<StatusCallback>()
const mappingSubscribers = new Set<MappingCallback>()
const gatewayLogSubscribers = new Set<GatewayLogCallback>()

export function useSignalR(options: UseSignalROptions = {}): UseSignalRReturn {
  const [connectionState, setConnectionState] = useState<HubConnectionState>(
    sharedConnection?.state ?? HubConnectionState.Disconnected
  )

  // Store callbacks in refs to avoid dependency issues
  const optionsRef = useRef(options)
  optionsRef.current = options

  const token = localStorage.getItem('octoporty_token')

  useEffect(() => {
    if (!token) return

    // Register this component's callbacks
    const statusCallback: StatusCallback = (update) => {
      optionsRef.current.onStatusUpdate?.(update)
    }
    const mappingCallback: MappingCallback = (update) => {
      optionsRef.current.onMappingUpdate?.(update)
    }
    const gatewayLogCallback: GatewayLogCallback = (log) => {
      optionsRef.current.onGatewayLog?.(log)
    }

    statusSubscribers.add(statusCallback)
    mappingSubscribers.add(mappingCallback)
    gatewayLogSubscribers.add(gatewayLogCallback)
    subscriberCount++

    // Create connection if this is the first subscriber
    if (!sharedConnection && !connectionPromise) {
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

      // Register event handlers that broadcast to all subscribers
      connection.on('StatusUpdate', (update: StatusUpdate) => {
        statusSubscribers.forEach((cb) => cb(update))
      })

      connection.on('MappingStatusUpdate', (update: MappingStatusUpdate) => {
        mappingSubscribers.forEach((cb) => cb(update))
      })

      connection.on('GatewayLog', (log: GatewayLog) => {
        gatewayLogSubscribers.forEach((cb) => cb(log))
      })

      connection.onreconnecting((error) => {
        console.log('SignalR reconnecting...', error)
      })

      connection.onreconnected((connectionId) => {
        console.log('SignalR reconnected:', connectionId)
      })

      connection.onclose((error) => {
        console.log('SignalR disconnected:', error)
        sharedConnection = null
        connectionPromise = null
      })

      sharedConnection = connection

      connectionPromise = connection
        .start()
        .then(() => {
          console.log('SignalR connected')
          setConnectionState(HubConnectionState.Connected)
        })
        .catch((error) => {
          console.error('SignalR connection error:', error)
          setConnectionState(HubConnectionState.Disconnected)
          sharedConnection = null
          connectionPromise = null
        })
    } else if (sharedConnection?.state === HubConnectionState.Connected) {
      setConnectionState(HubConnectionState.Connected)
    }

    // Sync connection state periodically
    const stateInterval = setInterval(() => {
      if (sharedConnection) {
        setConnectionState(sharedConnection.state)
      }
    }, 1000)

    return () => {
      // Unsubscribe this component's callbacks
      statusSubscribers.delete(statusCallback)
      mappingSubscribers.delete(mappingCallback)
      gatewayLogSubscribers.delete(gatewayLogCallback)
      subscriberCount--
      clearInterval(stateInterval)

      // Close connection if this was the last subscriber
      if (subscriberCount === 0 && sharedConnection) {
        sharedConnection.stop()
        sharedConnection = null
        connectionPromise = null
      }
    }
  }, [token])

  return {
    connectionState,
    isConnected: connectionState === HubConnectionState.Connected,
  }
}
