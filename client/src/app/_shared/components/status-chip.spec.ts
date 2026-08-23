import { TestBed } from '@angular/core/testing';
import { BeltSwatch, StatusChip } from './status-chip';
import {
  ATTENDANCE_STATUS_PRESENTATION,
  NOTE_PRIORITY_PRESENTATION,
  TRAINING_STATUS_PRESENTATION,
} from '../status-presentation';
import { AttendanceStatus, NotePriority, TrainingStatus } from '../../_models/enums';

describe('StatusChip', () => {
  function render(label: string, tone: string, icon: string): HTMLElement {
    const fixture = TestBed.createComponent(StatusChip);
    fixture.componentRef.setInput('label', label);
    fixture.componentRef.setInput('tone', tone);
    fixture.componentRef.setInput('icon', icon);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('carries the state as a glyph as well as a colour', () => {
    const host = render('Present', 'positive', 'check_circle');

    // Colour alone fails a colour-blind reader and a printed page, so the icon has to be
    // there and it has to be the state's own icon.
    expect(host.querySelector('mat-icon')?.textContent?.trim()).toBe('check_circle');
    expect(host.textContent).toContain('Present');
  });

  it('puts the tone on the chip so the token pair applies', () => {
    expect(render('Absent', 'critical', 'cancel').querySelector('.chip-critical')).not.toBeNull();
  });
});

describe('status presentation maps', () => {
  it('covers every training status', () => {
    for (const status of [
      TrainingStatus.Active,
      TrainingStatus.Cancelled,
      TrainingStatus.Finished,
    ]) {
      expect(TRAINING_STATUS_PRESENTATION[status]).toBeDefined();
    }
  });

  it('uses the colours SPEC 6.5 names: green finished, yellow active', () => {
    expect(TRAINING_STATUS_PRESENTATION[TrainingStatus.Finished].tone).toBe('positive');
    expect(TRAINING_STATUS_PRESENTATION[TrainingStatus.Active].tone).toBe('caution');
  });

  it('gives every state a distinct icon within its own map', () => {
    for (const map of [
      TRAINING_STATUS_PRESENTATION,
      ATTENDANCE_STATUS_PRESENTATION,
      NOTE_PRIORITY_PRESENTATION,
    ]) {
      const icons = Object.values(map).map((entry) => entry.icon);
      expect(new Set(icons).size).toBe(icons.length);
    }
  });

  it('reads High priority as the most urgent tone', () => {
    expect(NOTE_PRIORITY_PRESENTATION[NotePriority.High].tone).toBe('critical');
    expect(NOTE_PRIORITY_PRESENTATION[NotePriority.Low].tone).toBe('info');
  });

  it('marks an absent member critical and an invited one merely informational', () => {
    expect(ATTENDANCE_STATUS_PRESENTATION[AttendanceStatus.Absent].tone).toBe('critical');
    expect(ATTENDANCE_STATUS_PRESENTATION[AttendanceStatus.Invited].tone).toBe('info');
  });
});

describe('BeltSwatch', () => {
  function swatchColour(beltName: string): string {
    const fixture = TestBed.createComponent(BeltSwatch);
    fixture.componentRef.setInput('beltName', beltName);
    fixture.detectChanges();

    const dot = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.belt-dot');
    return dot?.style.background ?? '';
  }

  it('reads the colour out of the belt name', () => {
    expect(swatchColour('Green')).toContain('--tcm-belt-green');
    expect(swatchColour('Black')).toContain('--tcm-belt-black');
  });

  it('handles the striped belts the seeder creates', () => {
    expect(swatchColour('Yellow Stripe')).toContain('--tcm-belt-yellow');
  });

  it('falls back rather than vanishing on a belt it does not know', () => {
    // Belts are seeded rows, so a club could add one this map has never heard of.
    expect(swatchColour('Puce')).toContain('--tcm-quiet');
  });
});
