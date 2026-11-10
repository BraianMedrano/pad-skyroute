"use client";

import { useEffect, useRef } from "react";
import type { StyleSpecification } from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";

// Plaza Independencia, San Miguel de Tucumán (lng, lat).
const DEMO_CENTER: [number, number] = [-65.2039, -26.8305];
const DEMO_ZOOM = 14;

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
  ],
};

export function FlightMap() {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let map: import("maplibre-gl").Map | undefined;
    let cancelled = false;

    // MapLibre needs the browser, so it is loaded only on the client.
    import("maplibre-gl").then(({ Map, setWorkerUrl }) => {
      if (cancelled || !containerRef.current) return;
      // MapLibre looks for its worker next to its own module, which the bundler moves,
      // so point it at the worker file the bundler emits.
      setWorkerUrl(new URL("maplibre-gl/dist/maplibre-gl-worker.mjs", import.meta.url).href);
      map = new Map({
        container: containerRef.current,
        style: MAP_STYLE,
        center: DEMO_CENTER,
        zoom: DEMO_ZOOM,
      });
    });

    return () => {
      cancelled = true;
      map?.remove();
    };
  }, []);

  return <div ref={containerRef} className="h-full w-full" aria-label="Operations map" />;
}
