import {
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  viewChild,
} from '@angular/core';
import Chart, { ChartConfiguration, ChartType } from 'chart.js/auto';

/**
 * The one way charts are drawn in this app (SPEC sections 6.2 and 6.4).
 *
 * Chart.js is used directly rather than through an Angular wrapper library: wrappers pin a
 * peer range against the Angular major and lag a new release, and this app is on Angular 22.
 * Chart.js itself is framework-agnostic, so there is nothing to lag.
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
    <p class="chart-fallback">{{ ariaLabel() }}</p>
  `,
  styles: `
    .chart-host {
      position: relative;
      inline-size: 100%;
      block-size: 100%;
      min-block-size: 12rem;
    }

    /* Visible only to assistive technology: the same summary the canvas carries. */
    .chart-fallback {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class ChartComponent implements OnDestroy {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartConfiguration['data']>();
  readonly options = input<ChartConfiguration['options']>({});
  readonly ariaLabel = input.required<string>();

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart;

  constructor() {
    effect(() => {
      // Read every input so the effect re-runs when any of them changes.
      const type = this.type();
      const data = this.data();
      const options = this.options();
      const element = this.canvas().nativeElement;

      // Chart.js mutates the config it is given, so it is rebuilt rather than patched. These
      // charts hold tens of points, not thousands; correctness is worth more than the redraw.
      this.chart?.destroy();
      this.chart = new Chart(element, {
        type,
        data,
        options: {
          responsive: true,
          maintainAspectRatio: false,
          ...options,
        },
      });
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }
}
