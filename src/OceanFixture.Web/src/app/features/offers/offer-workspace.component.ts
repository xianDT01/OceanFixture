import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, map, of, switchMap } from 'rxjs';
import { CargoOffer } from '../../core/models';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';

@Component({ selector: 'of-offer-workspace', standalone: true, imports: [AsyncPipe, RouterLink], templateUrl: './offer-workspace.component.html', styleUrl: './offer-workspace.component.scss' })
export class OfferWorkspaceComponent {
  private readonly route = inject(ActivatedRoute); private readonly api = inject(OceanFixtureApiService);
  readonly isNew = this.route.snapshot.paramMap.get('id') === null;
  readonly offer$: Observable<CargoOffer | undefined> = this.route.paramMap.pipe(map(params => params.get('id')), switchMap(id => id ? this.api.offer(id) : of(undefined)));
  step = 1;
  readonly owners = [
    { id: 119, name: 'Northstar Shipping', country: 'Germany' },
    { id: 120, name: 'Baltic Meridian', country: 'Lithuania' },
    { id: 121, name: 'Bluehaven Carriers', country: 'Spain' },
    { id: 122, name: 'Atlantic Crest', country: 'Spain' }
  ];
  readonly selectedOwners = new Set<number>();

  setStep(step: number): void { this.step = step; }
  toggleOwner(id: number, checked: boolean): void { checked ? this.selectedOwners.add(id) : this.selectedOwners.delete(id); }
  initials(name: string): string { return name.split(' ').map(part => part[0]).join('').slice(0, 2); }
}
