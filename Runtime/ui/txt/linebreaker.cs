﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

namespace Unity.UIWidgets.ui {

    public class TabStops {

        int _tabWidth;
        List<int> _stops = new List<int>();
        
        public void set(List<int> stops, int tabWidth) {
            this._stops.Clear();
            if (stops != null) {
                this._stops.AddRange(stops);
            }

            this._tabWidth = tabWidth;
        }
        public float nextTab(float widthSoFar) {
            for (int i = 0; i < this._stops.Count; i++) {
                if (this._stops[i] > widthSoFar) {
                    return this._stops[i];
                }
            }
            return (float)(Math.Floor(widthSoFar / this._tabWidth + 1) * this._tabWidth);
        }
    }

    public class Candidate {
        public int offset;
        public int pre;
        public double preBreak;
        public float penalty;
        
        public double postBreak;
        public int preSpaceCount;
        public int postSpaceCount;
    }

    public class LineBreaker {

        const float ScoreInfty = float.MaxValue;
        const float ScoreDesperate = 1e10f;
        
        string _textBuf;
        int _textOffset;
        int _textLength;
        List<float> _charWidths = new List<float>();
        List<int> _breaks = new List<int>();
        List<float> _widths = new List<float>();
        WordBreaker _wordBreaker = new WordBreaker();
        double _width = 0.0;
        double _preBreak;
        double _lineWidth;
        int _lastBreak;
        int _bestBreak;
        float _bestScore;
        int _spaceCount;
        TabStops _tabStops = new TabStops();
        int mFirstTabIndex;
        List<Candidate> _candidates = new List<Candidate>();
        
        public int computeBreaks() {
            int nCand = this._candidates.Count;
            if (nCand > 0 && (nCand == 1 || this._lastBreak != nCand - 1)) {
                var cand = this._candidates[this._candidates.Count - 1];
                this._pushBreak(cand.offset, (float)(cand.postBreak - this._preBreak));
            }
            return this._breaks.Count;
        }

        public List<int> getBreaks() {
            return this._breaks;
        }

        public void resize(int size) {
            if (this._charWidths.Count < size) {
                this._charWidths.AddRange(Enumerable.Repeat(0.0f, size - this._charWidths.Count));
            }
        }
        public void setText(string text, int textOffset, int textLength) {
            this._textBuf = text;
            this._textOffset = textOffset;
            this._textLength = textLength;
            this._wordBreaker.setText(this._textBuf, textOffset, textLength);
            this._wordBreaker.next();
            this._candidates.Clear();
            Candidate can = new Candidate {
                offset = 0, postBreak = 0, preBreak = 0, postSpaceCount = 0, preSpaceCount = 0, pre = 0
            };
            this._candidates.Add(can);
            this._lastBreak = 0;
            this._bestBreak = 0;
            this._bestScore = ScoreInfty;
            this._preBreak = 0;
            this.mFirstTabIndex = int.MaxValue;
            this._spaceCount = 0;
        }

        public void setLineWidth(float lineWidth) {
            this._lineWidth = lineWidth;
        }

        public float addStyleRun(TextStyle style, int start, int end) {
            float width = 0.0f;
            if (style != null) {
                width = Layout.measureText(this._textBuf, start + this._textOffset, end - start, style,
                    this._charWidths, start);
            }

            int current = this._wordBreaker.current();
            int afterWord = start;
            int lastBreak = start;

            double lastBreakWidth = this._width;
            double postBreak = this._width;
            int postSpaceCount = this._spaceCount;

            for (int i = start; i < end; i++) {
                char c = this._textBuf[i + this._textOffset];
                if (c == '\t') {
                    this._width = this._preBreak + this._tabStops.nextTab((float)(this._width - this._preBreak));
                    if (this.mFirstTabIndex == Int32.MaxValue) {
                        this.mFirstTabIndex = i;
                    }
                }
                else {
                    if (LayoutUtils.isWordSpace(c)) {
                        this._spaceCount += 1;
                    }

                    this._width += this._charWidths[i];
                    if (!LayoutUtils.isLineEndSpace(c)) {
                        postBreak = this._width;
                        postSpaceCount = this._spaceCount;
                        afterWord = i + 1;
                    }
                }

                if (i + 1 == current) {
                    int wordStart = this._wordBreaker.wordStart();
                    int wordEnd = this._wordBreaker.wordEnd();
                    if (style != null || current == end || this._charWidths[current] > 0) {
                        this._addWordBreak(current, this._width, postBreak, this._spaceCount, postSpaceCount, 0);   
                    }

                    lastBreak = current;
                    lastBreakWidth = this._width;
                    current = this._wordBreaker.next();
                }
            }
            return width;
        }

        public void finish() {
            this._wordBreaker.finish();
            this._width = 0;
            this._candidates.Clear();
            this._widths.Clear();
            this._breaks.Clear();
            this._textBuf = null;
        }

        public List<float> getWidths() {
            return this._widths;
        }

        public void setTabStops(List<int> stops, int tabWidth) {
            this._tabStops.set(stops, tabWidth);
        }

        void _addWordBreak(int offset, double preBreak, double postBreak, int preSpaceCount, int postSpaceCount, float penalty) {
            Candidate cand = new Candidate();
            double width = this._candidates[this._candidates.Count - 1].preBreak;
            if (postBreak - width > this._lineWidth) {
                int i = this._candidates[this._candidates.Count - 1].offset;
                width += this._charWidths[i++];
                for (; i < offset; i++) {
                    float w = this._charWidths[i];
                    if (w > 0) {
                        cand.offset = i;
                        cand.preBreak = width;
                        cand.postBreak = width;
                        cand.preSpaceCount = postSpaceCount;
                        cand.preSpaceCount = postSpaceCount;
                        cand.penalty = ScoreDesperate;
                        this._addCandidate(cand);
                        width += w;

                    }
                }
            }
            
            cand.offset = offset;
            cand.preBreak = preBreak;
            cand.postBreak = postBreak;
            cand.penalty = penalty;
            cand.preSpaceCount = preSpaceCount;
            cand.preSpaceCount = postSpaceCount;
            this._addCandidate(cand);
        }

        

        void _addCandidate(Candidate cand) {
            int candIndex = this._candidates.Count;
            this._candidates.Add(cand);
            if (cand.postBreak - this._preBreak > this._lineWidth) {
                this._pushGreedyBreak();
            }

            if (cand.penalty <= this._bestScore) {
                this._bestBreak = candIndex;
                this._bestScore = cand.penalty;
            }
        }
        
        void _pushGreedyBreak() {
            var bestCandidate = this._candidates[this._bestBreak];
            this._pushBreak(bestCandidate.offset, (float)(bestCandidate.postBreak - this._preBreak));
            this._bestScore = ScoreInfty;
            this._lastBreak = this._bestBreak;
            this._preBreak = bestCandidate.preBreak;
        }

        void _pushBreak(int offset, float width) {
            this._breaks.Add(offset);
            this._widths.Add(width);
        }
        
    }
}
