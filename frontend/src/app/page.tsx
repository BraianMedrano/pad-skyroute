import { FlightMap } from "@/components/FlightMap";

export default function Home() {
  return (
    <div className="grid h-dvh grid-rows-[auto_minmax(0,1fr)]">
      <header className="flex items-center gap-x-6 border-b border-line bg-panel px-[18px] py-3">
        <span className="flex items-center gap-2.5 font-display text-xl font-bold tracking-[0.2em] uppercase before:size-2 before:bg-amber before:content-['']">
          SkyRoute
        </span>
        <span className="ml-auto font-mono text-xs text-muted">San Miguel de Tucumán</span>
      </header>

      <main className="grid min-h-0 grid-rows-[auto_minmax(320px,1fr)_auto] min-[960px]:grid-cols-[230px_minmax(0,1fr)_250px] min-[960px]:grid-rows-1">
        <aside
          aria-label="Fleet"
          className="border-b border-line bg-panel p-3.5 min-[960px]:border-r min-[960px]:border-b-0"
        >
          <span className="label">Fleet</span>
        </aside>

        <section className="min-h-80 min-w-0">
          <FlightMap />
        </section>

        <aside
          aria-label="Details"
          className="border-t border-line bg-panel p-4 min-[960px]:border-t-0 min-[960px]:border-l"
        >
          <span className="label">Details</span>
        </aside>
      </main>
    </div>
  );
}
