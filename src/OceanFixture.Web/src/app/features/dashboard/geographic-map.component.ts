import { AfterViewInit, Component, ElementRef, InjectionToken, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';
import * as Leaflet from 'leaflet/dist/leaflet-src.esm';

// Injectable adapter keeps unit tests independent of external map servers.
export const LEAFLET = new InjectionToken<Pick<typeof Leaflet, 'map' | 'tileLayer'>>('Leaflet', {
  providedIn: 'root', factory: () => ({ map: Leaflet.map, tileLayer: Leaflet.tileLayer })
});

@Component({
  selector: 'of-geographic-map', standalone: true,
  template: `
    <div #container class="geographic-map" role="region" aria-label="Interactive world map. Use arrow keys to pan and plus or minus to zoom."></div>
    @if (tileError) {
      <div class="map-error" role="status">Map tiles could not be loaded. Check your internet connection.
        <button type="button" (click)="retry()">RETRY</button>
      </div>
    }
  `,
  styles: [`
    :host{position:absolute;inset:0;display:block;z-index:0;isolation:isolate}
    .geographic-map{width:100%;height:100%;background:#aad3df}
    .map-error{position:absolute;z-index:1000;bottom:28px;left:12px;right:84px;max-width:480px;padding:9px 12px;background:#fff4df;color:#684b17;border:1px solid #e3c48a;border-radius:3px;font-size:12px}
    .map-error button{margin-left:8px;border:1px solid #b59557;background:white;padding:3px 7px;cursor:pointer}
  `]
})
export class GeographicMapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('container', { static: true }) private container!: ElementRef<HTMLDivElement>;
  private readonly leaflet = inject(LEAFLET);
  private readonly zone = inject(NgZone);
  private map?: Leaflet.Map;
  private tiles?: Leaflet.TileLayer;
  private observer?: ResizeObserver;
  tileError = false;

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      this.map = this.leaflet.map(this.container.nativeElement, {
        zoomControl: false, minZoom: 2, maxZoom: 19, worldCopyJump: true,
        scrollWheelZoom: false, keyboard: true
      }).setView([37, -10], 3);
      this.tiles = this.leaflet.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a> contributors'
      });
      this.tiles.on('tileerror', () => this.zone.run(() => { this.tileError = true; }));
      this.tiles.addTo(this.map);
      this.observer = new ResizeObserver(() => this.refreshLayout());
      this.observer.observe(this.container.nativeElement);
    });
  }

  zoomIn(): void { this.zone.runOutsideAngular(() => this.map?.zoomIn()); }
  zoomOut(): void { this.zone.runOutsideAngular(() => this.map?.zoomOut()); }
  refreshLayout(): void { this.zone.runOutsideAngular(() => this.map?.invalidateSize({ pan: false })); }
  retry(): void { this.tileError = false; this.zone.runOutsideAngular(() => this.tiles?.redraw()); }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.tiles?.off();
    this.map?.remove();
    this.map = undefined;
  }
}
