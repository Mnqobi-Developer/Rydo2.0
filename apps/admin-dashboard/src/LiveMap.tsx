import { useEffect, useRef, useState } from 'react'
import { appConfig } from './config/environment'
import type { LiveDriver, Trip } from './types'

type GoogleMap = { setCenter(position: LatLng): void }
type LatLng = { lat: number; lng: number }
type MapsApi = {
  Map: new (element: HTMLElement, options: object) => GoogleMap
  Marker: new (options: object) => unknown
  Polyline: new (options: object) => unknown
  LatLngBounds: new () => { extend(position: LatLng): void; isEmpty(): boolean }
}

declare global {
  interface Window { google?: { maps: MapsApi } }
}

let mapsPromise: Promise<MapsApi> | null = null

function loadMaps() {
  if (window.google?.maps) return Promise.resolve(window.google.maps)
  if (!appConfig.googleMapsWebApiKey) return Promise.reject(new Error('Google Maps web key is not configured.'))
  mapsPromise ??= new Promise<MapsApi>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(appConfig.googleMapsWebApiKey)}&v=weekly`
    script.async = true
    script.onload = () => window.google?.maps ? resolve(window.google.maps) : reject(new Error('Google Maps failed to initialise.'))
    script.onerror = () => reject(new Error('Google Maps could not be loaded.'))
    document.head.appendChild(script)
  })
  return mapsPromise
}

export function LiveMap({ drivers, trips }: { drivers: LiveDriver[]; trips: Trip[] }) {
  const host = useRef<HTMLDivElement>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void loadMaps().then((maps) => {
      if (!active || !host.current) return
      host.current.replaceChildren()
      const center = drivers[0]
        ? { lat: drivers[0].latitude, lng: drivers[0].longitude }
        : { lat: -26.2041, lng: 28.0473 }
      const map = new maps.Map(host.current, {
        center,
        zoom: drivers.length ? 12 : 10,
        disableDefaultUI: true,
        zoomControl: true,
        styles: [{ featureType: 'poi', stylers: [{ visibility: 'off' }] }],
      })
      const bounds = new maps.LatLngBounds()
      for (const driver of drivers) {
        const position = { lat: driver.latitude, lng: driver.longitude }
        bounds.extend(position)
        new maps.Marker({ map, position, title: `${driver.displayName} · Online`, label: 'D' })
      }
      for (const trip of trips.filter((item) => !['Completed', 'Cancelled'].includes(item.status))) {
        const pickup = { lat: trip.pickupLatitude, lng: trip.pickupLongitude }
        const destination = { lat: trip.destinationLatitude, lng: trip.destinationLongitude }
        bounds.extend(pickup)
        bounds.extend(destination)
        new maps.Marker({ map, position: pickup, title: `Pickup · ${trip.pickupAddress}`, label: 'P' })
        new maps.Marker({ map, position: destination, title: `Drop-off · ${trip.destinationAddress}`, label: 'R' })
        new maps.Polyline({ map, path: [pickup, destination], strokeColor: '#2457FF', strokeOpacity: 0.75, strokeWeight: 4 })
      }
      setError('')
    }).catch((reason: unknown) => active && setError(reason instanceof Error ? reason.message : 'Map unavailable.'))
    return () => { active = false }
  }, [drivers, trips])

  return (
    <div className="live-map">
      <div ref={host} className="live-map-canvas" />
      {error && (
        <div className="map-fallback">
          <strong>Live coordinates connected</strong>
          <span>{error}</span>
          <div>{drivers.length} online driver{drivers.length === 1 ? '' : 's'} · {trips.filter((trip) => !['Completed', 'Cancelled'].includes(trip.status)).length} active trips</div>
        </div>
      )}
    </div>
  )
}
