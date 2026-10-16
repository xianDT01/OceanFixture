import { TestBed } from '@angular/core/testing';
import type { Map, TileLayer } from 'leaflet';
import { GeographicMapComponent, LEAFLET } from './geographic-map.component';

describe('GeographicMapComponent', () => {
  let map: jasmine.SpyObj<Map>;
  let tiles: jasmine.SpyObj<TileLayer>;
  let factory: { map: jasmine.Spy; tileLayer: jasmine.Spy };
  let tileError: () => void;

  beforeEach(async () => {
    map = jasmine.createSpyObj('map', ['setView', 'zoomIn', 'zoomOut', 'invalidateSize', 'remove']);
    map.setView.and.returnValue(map);
    tiles = jasmine.createSpyObj('tiles', ['on', 'addTo', 'off', 'redraw']);
    tiles.on.and.callFake(((event: string, handler: () => void) => {
      if (event === 'tileerror') tileError = handler;
      return tiles;
    }) as unknown as TileLayer['on']);
    factory = { map: jasmine.createSpy('createMap').and.returnValue(map),
      tileLayer: jasmine.createSpy('createTiles').and.returnValue(tiles) };
    await TestBed.configureTestingModule({ imports: [GeographicMapComponent],
      providers: [{ provide: LEAFLET, useValue: factory }] }).compileComponents();
  });

  it('initializes geographic tiles with visible attribution and no tracking data', () => {
    const fixture = TestBed.createComponent(GeographicMapComponent);
    fixture.detectChanges();
    expect(map.setView).toHaveBeenCalledWith([37, -10], 3);
    expect(factory.tileLayer.calls.mostRecent().args[0]).toBe('https://tile.openstreetmap.org/{z}/{x}/{y}.png');
    expect(factory.tileLayer.calls.mostRecent().args[1].attribution).toContain('https://www.openstreetmap.org/copyright');
    expect(tiles.addTo).toHaveBeenCalledWith(map);
    expect(fixture.nativeElement.querySelector('[role=region]').getAttribute('aria-label')).toContain('Interactive world map');
  });

  it('connects zoom controls and recalculates size when the container changes', () => {
    const fixture = TestBed.createComponent(GeographicMapComponent);
    fixture.detectChanges();
    fixture.componentInstance.zoomIn(); fixture.componentInstance.zoomOut(); fixture.componentInstance.refreshLayout();
    expect(map.zoomIn).toHaveBeenCalledTimes(1);
    expect(map.zoomOut).toHaveBeenCalledTimes(1);
    expect(map.invalidateSize).toHaveBeenCalledWith({ pan: false });
  });

  it('shows a connection error and lets the user retry the tile layer', () => {
    const fixture = TestBed.createComponent(GeographicMapComponent);
    fixture.detectChanges();
    tileError(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('internet connection');
    fixture.nativeElement.querySelector('button').click(); fixture.detectChanges();
    expect(tiles.redraw).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.tileError).toBeFalse();
  });

  it('disconnects resizing, events and map resources when leaving the dashboard', () => {
    const disconnect = spyOn(ResizeObserver.prototype, 'disconnect').and.callThrough();
    const fixture = TestBed.createComponent(GeographicMapComponent);
    fixture.detectChanges(); fixture.destroy();
    expect(disconnect).toHaveBeenCalled();
    expect(tiles.off).toHaveBeenCalledTimes(1);
    expect(map.remove).toHaveBeenCalledTimes(1);
  });
});
