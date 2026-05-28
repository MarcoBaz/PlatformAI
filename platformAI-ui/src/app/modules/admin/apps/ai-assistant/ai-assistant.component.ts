import { TextFieldModule } from '@angular/cdk/text-field';
import { DatePipe, NgClass } from '@angular/common';
import { AfterViewChecked, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, HostListener, NgZone, OnDestroy, OnInit, ViewChild, ViewEncapsulation, } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, takeUntil } from 'rxjs';
import { NgApexchartsModule } from 'ng-apexcharts';
import { TranslocoModule } from '@jsverse/transloco';
import { AiAssistantService } from './ai-assistant.service';
import { ConversationVM, MessageVM } from './ai-assistant.types';
import { ChartData } from './analytics-streaming.types';

@Component({
    selector: 'ai-assistant',
    templateUrl: './ai-assistant.component.html',
    styleUrls: ['./ai-assistant.component.scss'],
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [
        MatSidenavModule,
        MatButtonModule,
        MatIconModule,
        MatMenuModule,
        MatFormFieldModule,
        MatInputModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        TextFieldModule,
        FormsModule,
        NgClass,
        DatePipe,
        NgApexchartsModule,
        TranslocoModule,
    ],
})
export class AiAssistantComponent implements OnInit, OnDestroy, AfterViewChecked {
    @ViewChild('messageInput') messageInput: ElementRef;
    @ViewChild('messagesContainer') messagesContainer: ElementRef;
    @ViewChild('scrollAnchor') scrollAnchor: ElementRef;

    // Conversation data
    conversations: ConversationVM[] = [];
    currentConversation: ConversationVM | null = null;
    isLoading: boolean = false;
    isStreaming: boolean = false;

    // UI state
    messageText: string = '';
    drawerMode: 'over' | 'side' = 'side';
    drawerOpened: boolean = true;

    private _unsubscribeAll: Subject<any> = new Subject<any>();
    private _shouldScrollToBottom: boolean = false;
    private _chartCache = new Map<string, any>();

    constructor(
        private _changeDetectorRef: ChangeDetectorRef,
        private _aiAssistantService: AiAssistantService,
        private _ngZone: NgZone
    ) { }

    @HostListener('input')
    @HostListener('ngModelChange')
    protected _resizeMessageInput(): void {
        if (!this.messageInput) return;

        this._ngZone.runOutsideAngular(() => {
            setTimeout(() => {
                this.messageInput.nativeElement.style.height = 'auto';
                this._changeDetectorRef.detectChanges();
                this.messageInput.nativeElement.style.height = `${this.messageInput.nativeElement.scrollHeight}px`;
                this._changeDetectorRef.detectChanges();
            });
        });
    }

    // ── ApexCharts helpers ──────────────────────────────────────────────────
    // Ritorna opzioni cachate per chart ID: evita re-render ad ogni keystroke.

    getChartOptions(chart: ChartData): any {
        if (this._chartCache.has(chart.id)) {
            return this._chartCache.get(chart.id);
        }
        const opts = {
            series:  this._apexSeries(chart),
            chart:   this._apexChart(chart),
            xaxis:   this._apexXAxis(chart),
            labels:  this._apexLabels(chart),
            colors:  this._apexColors(chart),
            stroke:  this._apexStroke(chart),
            fill:    this._apexFill(chart),
            legend:      { show: true, position: 'bottom' },
            dataLabels:  { enabled: false },
            tooltip:     { enabled: true },
            grid:        { borderColor: '#f0f0f0' },
        };
        this._chartCache.set(chart.id, opts);
        return opts;
    }

    private _apexSeries(chart: ChartData): any {
        if (!chart?.datasets?.length) return [];
        if (chart.type === 'pie' || chart.type === 'doughnut') {
            return chart.datasets[0]?.data ?? [];
        }
        return chart.datasets.map(d => ({ name: d.label, data: d.data ?? [] }));
    }

    private _apexChart(chart: ChartData): any {
        const typeMap: Record<string, string> = {
            line: 'line', bar: 'bar', area: 'area',
            pie: 'pie', doughnut: 'donut', radar: 'radar', scatter: 'scatter',
        };
        return {
            type: typeMap[chart.type] ?? 'line',
            height: 240,
            toolbar: {
                show: true,
                tools: {
                    download: true,
                    selection: false,
                    zoom: false,
                    zoomin: false,
                    zoomout: false,
                    pan: false,
                    reset: false,
                }
            },
            animations: { enabled: false },
        };
    }

    private _apexXAxis(chart: ChartData): any {
        if (chart.type === 'pie' || chart.type === 'doughnut') return {};
        return {
            categories: chart.labels,
            labels: { rotate: -30, style: { fontSize: '11px' } },
        };
    }

    private _apexLabels(chart: ChartData): string[] {
        return (chart.type === 'pie' || chart.type === 'doughnut') ? chart.labels : [];
    }

    private _apexColors(chart: ChartData): string[] {
        return (chart.datasets ?? [])
            .map(d => d.borderColor || d.backgroundColor)
            .filter((c): c is string => !!c);
    }

    private _apexStroke(chart: ChartData): any {
        if (chart.type === 'bar' || chart.type === 'pie' || chart.type === 'doughnut') {
            return { show: false };
        }
        return { curve: 'smooth', width: 2 };
    }

    private _apexFill(chart: ChartData): any {
        if (chart.type === 'area') return { type: 'gradient', gradient: { opacityFrom: 0.4, opacityTo: 0.05 } };
        return { opacity: 1 };
    }

    // ───────────────────────────────────────────────────────────────────────────

    ngOnInit(): void {
        this._chartCache.clear();
        // Subscribe to conversations
        this._aiAssistantService.conversations$
            .pipe(takeUntil(this._unsubscribeAll))
            .subscribe((conversations) => {
                this.conversations = conversations;
                this._changeDetectorRef.markForCheck();
            });

        // Subscribe to current conversation
        this._aiAssistantService.currentConversation$
            .pipe(takeUntil(this._unsubscribeAll))
            .subscribe((conversation) => {
                if (conversation == null) {
                    conversation = this.conversations[0] || null;
                }
                this.currentConversation = conversation;
                this._shouldScrollToBottom = true;
                this._changeDetectorRef.markForCheck();
            });

        // Subscribe to loading state
        this._aiAssistantService.isLoading$
            .pipe(takeUntil(this._unsubscribeAll))
            .subscribe((isLoading) => {
                this.isLoading = isLoading;
                this._changeDetectorRef.markForCheck();
            });

        // Subscribe to streaming state
        this._aiAssistantService.isStreaming$
            .pipe(takeUntil(this._unsubscribeAll))
            .subscribe((isStreaming) => {
                this.isStreaming = isStreaming;
                this._shouldScrollToBottom = true;
                this._changeDetectorRef.markForCheck();
            });

        // Create initial conversation if none exists
        if (this.conversations.length === 0) {
            this._aiAssistantService.createNewConversation();
        }
    }

    ngAfterViewChecked(): void {
        if (this._shouldScrollToBottom) {
            this._scrollToBottom();
            this._shouldScrollToBottom = false;
        }
    }

    ngOnDestroy(): void {
        this._unsubscribeAll.next(null);
        this._unsubscribeAll.complete();
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Public methods
    // -----------------------------------------------------------------------------------------------------

    newConversation(): void {
        this._aiAssistantService.createNewConversation();
    }

    selectConversation(conversationId: string): void {
        const conversation = this.conversations.find((c) => c.id === conversationId);
        if (conversation) {
            conversation.messages.sort(
                (a, b) => new Date(a.lastModifiedDate).getTime() - new Date(b.lastModifiedDate).getTime()
            );

            this._aiAssistantService._currentConversation.next(conversation);

            // detectChanges() forza un ciclo di change detection sincrono:
            // Angular scrive i messaggi nel DOM prima che _scrollToBottom venga chiamato.
            // Con OnPush il normale markForCheck() è asincrono — il DOM non è ancora
            // aggiornato quando setTimeout(0) scatta, e scrollHeight risulta errato.
            this._changeDetectorRef.detectChanges();
            this._scrollToBottom();
        }
        if (this.drawerMode === 'over') {
            this.drawerOpened = false;
        }
    }

    deleteConversation(conversationId: string, event: Event): void {
        event.stopPropagation();
        this._aiAssistantService.deleteConversation(conversationId);
    }

    /**
     * Invia messaggio con streaming
     */
    async sendMessage(): Promise<void> {
        if (!this.messageText.trim() || this.isLoading) return;

        const message = this.messageText.trim();
        this.messageText = '';

        // Reset textarea height
        if (this.messageInput) {
            this.messageInput.nativeElement.style.height = 'auto';
        }

        // Usa il nuovo metodo con streaming
        await this._aiAssistantService.sendMessageWithStreaming(message);
        this._shouldScrollToBottom = true;
    }

    /**
     * Ferma lo streaming in corso
     */
    stopStreaming(): void {
        this._aiAssistantService.stopStreaming();
    }

    onKeyDown(event: KeyboardEvent): void {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            this.sendMessage();
        }
    }

    clearConversation(): void {
        this._aiAssistantService.clearCurrentConversation();
    }

    toggleDrawer(): void {
        this.drawerOpened = !this.drawerOpened;
    }

    trackByFn(index: number, item: any): any {
        return item.id || index;
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Private methods
    // -----------------------------------------------------------------------------------------------------

    private _scrollToBottom(): void {
        // scrollIntoView è più affidabile di scrollTop = scrollHeight:
        // funziona correttamente anche con OnPush e layout flex a altezza calcolata.
        this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'instant', block: 'end' });
    }
}
