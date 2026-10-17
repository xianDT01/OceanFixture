import { Component, inject } from '@angular/core';
import { SessionService } from '../core/session.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'of-shell', standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html', styleUrl: './shell.component.scss'
})
export class ShellComponent { readonly session = inject(SessionService); menuOpen = false; }
