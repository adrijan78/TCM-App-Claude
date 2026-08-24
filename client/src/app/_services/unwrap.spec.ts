import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage, apiErrorParts, describeFailure, unwrap } from './unwrap';

/**
 * Every screen in the app renders failures through these four functions, so what they return is
 * the app's entire error vocabulary. The rule under test throughout: never surface a raw
 * exception string, and keep the summary apart from the field rules because a form renders
 * them apart.
 */
describe('unwrap', () => {
  it('returns the data from a successful envelope', () => {
    expect(unwrap({ success: true, data: [1, 2], message: null, errors: [] })).toEqual([1, 2]);
  });

  it('throws the envelope message when success is false', () => {
    expect(() =>
      unwrap({ success: false, data: null, message: 'Not allowed.', errors: [] }),
    ).toThrow('Not allowed.');
  });

  it('throws when data is null even if success is true', () => {
    expect(() => unwrap({ success: true, data: null, message: null, errors: [] })).toThrow();
  });

  it('falls back to a generic message when the envelope has none', () => {
    expect(() => unwrap({ success: false, data: null, message: null, errors: [] })).toThrow(
      'The request could not be completed.',
    );
  });
});

describe('describeFailure', () => {
  it('joins the message and its field errors', () => {
    const line = describeFailure({
      success: false,
      data: null,
      message: 'Check the form.',
      errors: ['Email is required.', 'Password is too short.'],
    });

    expect(line).toBe('Check the form. Email is required. Password is too short.');
  });

  it('returns the message alone when there are no field errors', () => {
    expect(
      describeFailure({ success: false, data: null, message: 'Check the form.', errors: [] }),
    ).toBe('Check the form.');
  });
});

describe('apiErrorParts', () => {
  it('reads the message and errors out of an ApiResponse body', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { success: false, message: 'Check the form.', errors: ['Email is required.'] },
    });

    expect(apiErrorParts(error)).toEqual({
      message: 'Check the form.',
      details: ['Email is required.'],
    });
  });

  it('reports a network failure as unreachable rather than as status 0', () => {
    const error = new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') });

    expect(apiErrorParts(error).message).toContain('Cannot reach the server');
  });

  it('falls back when the body is not an ApiResponse', () => {
    const error = new HttpErrorResponse({ status: 500, error: '<html>Gateway Error</html>' });

    // Never the raw body: an HTML error page is not something to show a member.
    expect(apiErrorParts(error).message).toBe('Something went wrong. Please try again.');
  });

  it('uses a caller-supplied fallback', () => {
    const error = new HttpErrorResponse({ status: 500, error: null });

    expect(apiErrorParts(error, 'Could not load the member.').message).toBe(
      'Could not load the member.',
    );
  });

  it('reads the message off the Error that unwrap throws', () => {
    expect(apiErrorParts(new Error('Not allowed.')).message).toBe('Not allowed.');
  });

  it('falls back for a thrown value that is not an Error', () => {
    expect(apiErrorParts('something odd').message).toBe('Something went wrong. Please try again.');
  });

  it('ignores a non-array errors field', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { success: false, message: 'Check the form.', errors: 'not-an-array' },
    });

    expect(apiErrorParts(error).details).toEqual([]);
  });
});

describe('apiErrorMessage', () => {
  it('flattens the parts onto one line for a snackbar', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { success: false, message: 'Check the form.', errors: ['Email is required.'] },
    });

    expect(apiErrorMessage(error)).toBe('Check the form. Email is required.');
  });

  it('returns the message alone when there are no details', () => {
    expect(apiErrorMessage(new Error('Not allowed.'))).toBe('Not allowed.');
  });
});
