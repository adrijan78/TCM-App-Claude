import { Component, ElementRef, OnDestroy, effect, inject, input, viewChild } from '@angular/core';
import Chart, { ChartConfiguration, ChartType } from 'chart.js/auto';
import { ThemeService } from '../../_services/theme.service';
import { baseChartOptions, chartColour } from '../chart-theme';

/**
 * The one way charts are drawn in this app (SPEC sections 6.2 and 6.4).
 *
 * Chart.js is used directly rather than through an Angular wrapper library: wrappers pin a
 * peer range against the Angular major and lag a new release, and this app is on Angular 22.
 * Chart.js itself is framework-agnostic, so there is nothing to lag.
 *
 * Two things are handled here so no caller has to:
 *
 * - **Theme.** The chart is rebuilt whenever the light/dark choice changes, and its options
 *   come from `baseChartOptions()`, which reads the CSS tokens at build time. A chart drawn
 *   in light mode does not keep its light-mode gridlines after the toggle.
 * - **Colour.** A dataset that does not name its own colour is assigned one from the shared
 *   palette, in order. That is what keeps "trainings held" the same blue on every screen —
 *   and it means a caller normally passes labels and numbers, nothing else.
 *
 * `ariaLabel` is required, not optional. A bare `<canvas>` is invisible to a screen reader,
 * and every chart here is showing information that exists nowhere else on the screen.
 */
@Component({
  selector: 'app-chart',
  template: `
    <div class="chart-host">
      <canvas #canvas role="img" [attr.aria-label]="ariaLabel()"></canvas>
    </div>
    <p class="tcm-visually-hidden">{{ ariaLabel() }}</p>
  `,
  styles: `
    .chart-host {
      position: relative;
      inline-size: 100%;
      block-size: 100%;
      min-block-size: 12rem;
    }
  `,
})
export class ChartComponent implements OnDestroy {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartConfiguration['data']>();
  readonly options = input<ChartConfiguration['options']>({});
  readonly ariaLabel = input.required<string>();

  private readonly theme = inject(ThemeService);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart;

  constructor() {
    effect(() => {
      // Read every input so the effect re-runs when any of them changes — including the
      // resolved theme, which decides what the token lookups below return.
      const type = this.type();
      const data = this.data();
      const options = this.options();
      this.theme.resolved();

      const element = this.canvas().nativeElement;

      // Chart.js mutates the config it is given, so it is rebuilt rather than patched. These
      // charts hold tens of points, not thousands; correctness is worth more than the redraw.
      this.chart?.destroy();
      this.chart = new Chart(element, {
        type,
        data: this.withPalette(data),
        options: { ...baseChartOptions(), ...options },
      });
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  /**
   * Fills in the series colours a caller left unset. The input object is never mutated —
   * it belongs to the parent component, and Chart.js would otherwise write into its signal.
   */
  private withPalette(data: ChartConfiguration['data']): ChartConfiguration['data'] {
    return {
      ...data,
      datasets: data.datasets.map((dataset, index) => {
        const colour = chartColour(index);

        // The palette goes on first and the dataset spreads over it, so a caller that does
        // name its own colour keeps it.
        return { backgroundColor: colour, borderColor: colour, ...dataset };
      }),
    };
  }
}
