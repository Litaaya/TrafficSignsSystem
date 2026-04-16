import { Injectable } from '@angular/core';
import { HttpRequest, HttpHandler, HttpEvent, HttpInterceptor, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(private snackBar: MatSnackBar) { }

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        let errorMsg = 'An error occurred. Please try again later';

        if (error.error instanceof ErrorEvent) {
          errorMsg = `Network system error: ${error.error.message}`;
        } else {
          if (error.error && (error.error.Message || error.error.message)) {
            errorMsg = error.error.Message || error.error.message;
          }

          switch (error.status) {
            case 400:
              this.showErrorToast(`Invalid data: ${errorMsg}`);
              break;
            case 401:
              this.showErrorToast('Your login session has expired');
              break;
            case 403:
              this.showErrorToast('You are not authorized to perform this action.');
              break;
            case 404:
              this.showErrorToast(`No data found: ${errorMsg}`);
              break;
            case 500:
              this.showErrorToast(`Server error: (TraceId: ${error.error.TraceId || 'N/A'}).`);
              break;
            default:
              this.showErrorToast(errorMsg);
              break;
          }
        }

        return throwError(() => error);
      })
    );
  }

  private showErrorToast(message: string) {
    this.snackBar.open(message, 'Close', {
      duration: 5000,
      panelClass: ['error-snackbar'],
      horizontalPosition: 'right',
      verticalPosition: 'top'
    });
  }
}
