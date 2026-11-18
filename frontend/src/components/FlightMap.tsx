"use client";

import { useEffect, useRef, useState } from "react";
import type { GeoJSONSource, Map as MapLibreMap, Marker, StyleSpecification } from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import type { Coordinates } from "@/lib/api/client";
import type { TelemetryMessage } from "@/lib/telemetry";

// Plaza Independencia, San Miguel de Tucumán (lng, lat).
const DEMO_CENTER: [number, number] = [-65.2039, -26.8305];
const DEMO_ZOOM = 14;

const ROUTES_SOURCE = "mission-routes";
const SELECTED_SOURCE = "selected-route";

// OpenStreetMap raster tiles, inverted and desaturated to fit the dark theme.
const MAP_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    osm: {
      type: "raster",
      tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
      maxzoom: 19,
      attribution:
        '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    },
    [ROUTES_SOURCE]: { type: "geojson", data: { type: "FeatureCollection", features: [] } },
    [SELECTED_SOURCE]: { type: "geojson", data: { type: "FeatureCollection", features: [] } },
  },
  layers: [
    { id: "background", type: "background", paint: { "background-color": "#07111c" } },
    {
      id: "osm",
      type: "raster",
      source: "osm",
      paint: {
        "raster-brightness-min": 0.75,
        "raster-brightness-max": 0.08,
        "raster-saturation": -0.8,
        "raster-hue-rotate": 180,
        "raster-opacity": 0.8,
      },
    },
    {
      id: "mission-routes",
      type: "line",
      source: ROUTES_SOURCE,
      paint: { "line-color": "#f2a93b", "line-width": 2, "line-dasharray": [2, 2], "line-opacity": 0.8 },
    },
    {
      id: "selected-route",
      type: "line",
      source: SELECTED_SOURCE,
      paint: { "line-color": "#4fc3d6", "line-width": 3 },
    },
  ],
};

/** Origin and destination of the mission a drone is flying. */
export type MissionRoute = {
  missionId: string;
  origin: Coordinates;
  destination: Coordinates;
};

type FlightMapProps = {
  drones?: TelemetryMessage[];
  routes?: MissionRoute[];
  /** Mission to focus: its route is highlighted and framed together with its drone. */
  selected?: { route: MissionRoute; droneId: string | null };
  theme?: "day" | "night";
  picking?: boolean;
  origin?: Coordinates;
  destination?: Coordinates;
};

/** Map of the demo area. Fills its parent, so it works inside the app layout or full screen. */
export function FlightMap({ drones = [], routes = [], selected, theme = "night", picking = false, origin, destination }: FlightMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const markerClassRef = useRef<typeof Marker>(undefined);
  const markersRef = useRef<Record<string, Marker>>({});
  const draftMarkersRef = useRef<{ origin?: Marker; destination?: Marker }>({});
  const [map, setMap] = useState<MapLibreMap>();

  useEffect(() => {
    let instance: MapLibreMap | undefined;
    let cancelled = false;

    // MapLibre needs the browser, so it is loaded only on the client.
    import("maplibre-gl").then(({ Map, Marker, setWorkerUrl }) => {
      if (cancelled || !containerRef.current) return;
      // MapLibre looks for its worker next to its own module, which the bundler moves,
      // so point it at the worker file the bundler emits.
      setWorkerUrl(new URL("maplibre-gl/dist/maplibre-gl-worker.mjs", import.meta.url).href);
      markerClassRef.current = Marker;
      instance = new Map({
        container: containerRef.current,
        style: MAP_STYLE,
        center: DEMO_CENTER,
        zoom: DEMO_ZOOM,
      });
      const loaded = instance;
      loaded.on("load", () => setMap(loaded));
    });

    return () => {
      cancelled = true;
      instance?.remove();
      markersRef.current = {};
      draftMarkersRef.current = {};
      // Forget the removed map, so markers are not attached to it after a Fast Refresh.
      setMap(undefined);
    };
  }, []);

  useEffect(() => {
    if (!map) return;
    const day = theme === "day";
    map.setPaintProperty("background", "background-color", day ? "#edf2f4" : "#07111c");
    map.setPaintProperty("osm", "raster-brightness-min", day ? 0.1 : 0.75);
    map.setPaintProperty("osm", "raster-brightness-max", day ? 0.9 : 0.08);
    map.setPaintProperty("osm", "raster-saturation", day ? -0.15 : -0.8);
    map.setPaintProperty("osm", "raster-hue-rotate", day ? 0 : 180);
    map.setPaintProperty("osm", "raster-opacity", day ? 1 : 0.8);
  }, [map, theme]);

  useEffect(() => {
    if (!map) return;
    const handleClick = (event: import("maplibre-gl").MapMouseEvent) => {
      if (!picking) return;
      window.dispatchEvent(new CustomEvent("skyroute-map-point", { detail: { lat: event.lngLat.lat, lon: event.lngLat.wrap().lng } }));
    };
    map.on("click", handleClick);
    return () => { map.off("click", handleClick); };
  }, [map, picking]);

  useEffect(() => {
    if (!map || !containerRef.current) return;
    const observer = new ResizeObserver(() => map.resize());
    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, [map]);

  // One marker per drone, moved and rotated on every update.
  useEffect(() => {
    const MarkerClass = markerClassRef.current;
    if (!map || !MarkerClass) return;

    const markers = markersRef.current;
    const seen = new Set<string>();
    for (const drone of drones) {
      seen.add(drone.droneId);
      let marker = markers[drone.droneId];
      if (!marker) {
        // rotationAlignment "map" keeps the heading true to north when the map is rotated.
        marker = new MarkerClass({ element: createDroneElement(), rotationAlignment: "map" })
          .setLngLat([drone.lon, drone.lat])
          .addTo(map);
        markers[drone.droneId] = marker;
      }
      // The glyph points north, so the heading is the rotation as is.
      marker.setLngLat([drone.lon, drone.lat]).setRotation(drone.headingDeg);
      marker.getElement().dataset.status = drone.status;
      marker.getElement().dataset.selected = String(drone.droneId === selected?.droneId);
    }

    for (const id of Object.keys(markers)) {
      if (!seen.has(id)) {
        markers[id].remove();
        delete markers[id];
      }
    }
  }, [map, drones, selected?.droneId]);

  useEffect(() => {
    const MarkerClass = markerClassRef.current;
    if (!map || !MarkerClass) return;
    for (const endpoint of ["origin", "destination"] as const) {
      const point = endpoint === "origin" ? origin : destination;
      const current = draftMarkersRef.current[endpoint];
      if (!point) { current?.remove(); delete draftMarkersRef.current[endpoint]; continue; }
      if (!current) {
        const element = document.createElement("span");
        element.className = `draft-point draft-point-${endpoint}`;
        element.setAttribute("aria-label", endpoint === "origin" ? "Origen seleccionado" : "Destino seleccionado");
        draftMarkersRef.current[endpoint] = new MarkerClass({ element, color: endpoint === "origin" ? "#f2a93b" : "#4fc3d6" }).setLngLat([point.lon, point.lat]).addTo(map);
      } else current.setLngLat([point.lon, point.lat]);
    }
  }, [map, origin, destination]);

  // Straight line from origin to destination for every drone on a mission.
  useEffect(() => {
    const source = map?.getSource<GeoJSONSource>(ROUTES_SOURCE);
    source?.setData({
      type: "FeatureCollection",
      features: routes.map((route) => ({
        type: "Feature",
        properties: { missionId: route.missionId },
        geometry: {
          type: "LineString",
          coordinates: [
            [route.origin.lon, route.origin.lat],
            [route.destination.lon, route.destination.lat],
          ],
        },
      })),
    });
  }, [map, routes]);

  // Highlight the selected route and frame it, plus its drone once that one reports a position.
  // It reframes on a new selection or when the drone first appears, never on every tick, so the user can still pan.
  const selectedRoute = selected?.route;
  const selectedMissionId = selectedRoute?.missionId;
  const selectedDrone = drones.find((drone) => drone.droneId === selected?.droneId);
  const hasDrone = selectedDrone !== undefined;
  const dronePosition = useRef(selectedDrone);
  dronePosition.current = selectedDrone;
  const routeRef = useRef(selectedRoute);
  routeRef.current = selectedRoute;

  useEffect(() => {
    const source = map?.getSource<GeoJSONSource>(SELECTED_SOURCE);
    source?.setData({
      type: "FeatureCollection",
      features: selectedRoute
        ? [
            {
              type: "Feature",
              properties: {},
              geometry: {
                type: "LineString",
                coordinates: [
                  [selectedRoute.origin.lon, selectedRoute.origin.lat],
                  [selectedRoute.destination.lon, selectedRoute.destination.lat],
                ],
              },
            },
          ]
        : [],
    });
  }, [map, selectedRoute]);

  useEffect(() => {
    const route = routeRef.current;
    if (!map || !route) return;
    const points: [number, number][] = [
      [route.origin.lon, route.origin.lat],
      [route.destination.lon, route.destination.lat],
    ];
    const drone = dronePosition.current;
    if (drone) points.push([drone.lon, drone.lat]);
    const lons = points.map(([lon]) => lon);
    const lats = points.map(([, lat]) => lat);
    map.fitBounds(
      [
        [Math.min(...lons), Math.min(...lats)],
        [Math.max(...lons), Math.max(...lats)],
      ],
      { padding: 70, maxZoom: 16, duration: 600 },
    );
  }, [map, selectedMissionId, hasDrone]);

  return <div ref={containerRef} className={`h-full w-full ${picking ? "map-picking" : ""}`} aria-label={picking ? "Mapa; elegí un punto para la entrega" : "Mapa de operaciones"} />;
}

function createDroneElement(): HTMLElement {
  const element = document.createElement("div");
  element.className = "drone-marker";
  element.innerHTML =
    '<svg viewBox="-12 -12 24 24" width="24" height="24" aria-hidden="true"><path d="M0,-10 L7,8 L0,4 L-7,8 Z"/></svg>';
  return element;
}
