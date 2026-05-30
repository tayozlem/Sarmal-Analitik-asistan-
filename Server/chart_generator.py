import matplotlib
matplotlib.use('Agg') 
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
from scipy.interpolate import make_interp_spline
import io, base64, pandas as pd, numpy as np

# ================= MODÜLER ÇİZİM FONKSİYONLARI =================

def draw_scatter(ax, x_data, y_data, z_data, c_data, s_data, cmap, is_bubble):
    if is_bubble:
        if isinstance(s_data, np.ndarray):
            s_min, s_max = s_data.min(), s_data.max()
            if s_max > s_min:
                normalized_s = (s_data - s_min) / (s_max - s_min)
                bubble_sizes = (normalized_s ** 2.0) * 2000 + 50 
            else:
                bubble_sizes = s_data * 6.0
        else:
            bubble_sizes = s_data * 6.0

        if cmap and not isinstance(c_data, str):
            norm = plt.Normalize(c_data.min(), c_data.max())
            try: base_colors = plt.cm.get_cmap(cmap)(norm(c_data))
            except: base_colors = plt.get_cmap(cmap)(norm(c_data))
            
            face_colors = base_colors.copy()
            face_colors[:, 3] = 0.60
            edge_colors = base_colors.copy()
            edge_colors[:, 3] = 1.0   
            
            ax.scatter(x_data, y_data, z_data, facecolors=face_colors, edgecolors=edge_colors, s=bubble_sizes, linewidth=1.5, zorder=4)
        else:
            ax.scatter(x_data, y_data, z_data, color=c_data, s=bubble_sizes, alpha=0.60, edgecolors=c_data, linewidth=1.5, zorder=4)
    else:
        ax.scatter(x_data, y_data, z_data, c=c_data, s=45, cmap=cmap, alpha=0.75, edgecolors='white', linewidth=0.3, zorder=3)

def draw_voxel(ax, x_data, y_data, z_data, cmap):
    xyz = np.vstack([x_data, y_data, z_data]).T
    bins = [10, 10, 10]
    H, edges = np.histogramdd(xyz, bins=bins)
    
    x_edges, y_edges, z_edges = edges
    X, Y, Z = np.meshgrid(x_edges, y_edges, z_edges, indexing='ij')
    
    filled = H > 0 
    if filled.any():
        actual_cmap = cmap if cmap else 'plasma'
        norm = plt.Normalize(vmin=H[filled].min(), vmax=H[filled].max())
        color_map = plt.get_cmap(actual_cmap)
        
        colors = np.empty(H.shape + (4,))
        colors[filled] = color_map(norm(H[filled]))
        colors[filled, 3] = 0.6 
        
        ax.voxels(X, Y, Z, filled, facecolors=colors, edgecolor='black', linewidth=0.2)
        sm = plt.cm.ScalarMappable(cmap=color_map, norm=norm)
        sm.set_array([])
        return sm
    return None

def draw_line(ax, x_col, y_col, z_col, color_col, plot_df, z_min_global):
    if color_col and color_col in plot_df.columns:
        unique_colors = plot_df[color_col].unique()
        c_min, c_max = unique_colors.min(), unique_colors.max()
        for uc in unique_colors:
            group_df = plot_df[plot_df[color_col] == uc]
            line_df = group_df[[x_col, y_col, z_col]].groupby(x_col, as_index=False).mean(numeric_only=True).sort_values(by=x_col)
            lx, ly, lz = np.array(line_df[x_col]), np.array(line_df[y_col]), np.array(line_df[z_col])
            
            if len(lx) == 0: continue
            color_val = plt.cm.plasma((uc - c_min) / (c_max - c_min)) if c_max != c_min else plt.cm.plasma(0.7)
            
            if len(lx) > 3:
                try:
                    x_new = np.linspace(lx.min(), lx.max(), 150)
                    spl_y = make_interp_spline(lx, ly, k=3)
                    spl_z = make_interp_spline(lx, lz, k=3)
                    lx_plot, ly_plot, lz_plot = x_new, spl_y(x_new), spl_z(x_new)
                except:
                    lx_plot, ly_plot, lz_plot = lx, ly, lz
            else: lx_plot, ly_plot, lz_plot = lx, ly, lz
            
            ax.plot(lx_plot, ly_plot, lz_plot, color=color_val, linewidth=3.0, alpha=1.0)
            if len(lx_plot) > 1:
                verts = []
                for i in range(len(lx_plot)): verts.append((lx_plot[i], ly_plot[i], z_min_global))
                for i in range(len(lx_plot)-1, -1, -1): verts.append((lx_plot[i], ly_plot[i], lz_plot[i]))
                poly = Poly3DCollection([verts], facecolors=color_val, alpha=0.15, edgecolors=color_val, linewidths=0.5)
                ax.add_collection3d(poly)
    else:
        line_df = plot_df[[x_col, y_col, z_col]].groupby(x_col, as_index=False).mean(numeric_only=True).sort_values(by=x_col)
        lx, ly, lz = np.array(line_df[x_col]), np.array(line_df[y_col]), np.array(line_df[z_col])
        color_val = '#00B8D4' 
        
        if len(lx) > 3:
            try:
                x_new = np.linspace(lx.min(), lx.max(), 150)
                spl_y = make_interp_spline(lx, ly, k=3)
                spl_z = make_interp_spline(lx, lz, k=3)
                lx_plot, ly_plot, lz_plot = x_new, spl_y(x_new), spl_z(x_new)
            except:
                lx_plot, ly_plot, lz_plot = lx, ly, lz
        else: lx_plot, ly_plot, lz_plot = lx, ly, lz
            
        ax.plot(lx_plot, ly_plot, lz_plot, color=color_val, linewidth=3.5, alpha=1.0)

def draw_network(ax, x_col, y_col, z_col, color_col, plot_df, cmap):
    raw_cols = [x_col, y_col, z_col, color_col]
    group_cols = list(dict.fromkeys([c for c in raw_cols if c and c in plot_df.columns]))
    
    freq_df = plot_df.groupby(group_cols, dropna=False).size().reset_index(name='count')
    
    ux = np.array(freq_df[x_col], dtype=float)
    uy = np.array(freq_df[y_col], dtype=float)
    uz = np.array(freq_df[z_col], dtype=float)
    counts = np.array(freq_df['count'], dtype=float)
    
    c_min, c_max = counts.min(), counts.max()
    if c_max > c_min:
        node_sizes = ((counts - c_min) / (c_max - c_min)) * 800 + 40 
    else:
        node_sizes = counts * 10.0 + 40
        
    if color_col and color_col in freq_df.columns:
        uc = np.array(freq_df[color_col], dtype=float)
        colors = plt.cm.plasma(plt.Normalize(uc.min(), uc.max())(uc))
    else:
        uc = np.zeros(len(ux))
        colors = np.array(['#00E676'] * len(ux))
        
    ax.scatter(ux, uy, uz, c=colors, s=node_sizes, alpha=0.6, edgecolors='white', linewidth=0.5, zorder=4)

    unique_colors = np.unique(uc)
    hubs = []
    
    for c_val in unique_colors:
        idx = np.where(uc == c_val)[0]
        if len(idx) == 0: continue
        
        hub_x, hub_y, hub_z = np.mean(ux[idx]), np.mean(uy[idx]), np.mean(uz[idx])
        hubs.append([hub_x, hub_y, hub_z])
        hub_color = colors[idx[0]]
        
        ax.scatter(hub_x, hub_y, hub_z, c=[hub_color], s=300, marker='D', alpha=1.0, edgecolors='black', linewidth=1.5, zorder=5)
        
        for i in idx:
            ax.plot([ux[i], hub_x], [uy[i], hub_y], [uz[i], hub_z], color=hub_color, alpha=0.25, linewidth=1.0, zorder=1)

    if len(hubs) > 1:
        hubs = np.array(hubs)
        global_hub_x, global_hub_y, global_hub_z = np.mean(hubs[:,0]), np.mean(hubs[:,1]), np.mean(hubs[:,2])
        for h in hubs:
            ax.plot([h[0], global_hub_x], [h[1], global_hub_y], [h[2], global_hub_z], color='#9E9E9E', alpha=0.5, linewidth=2.0, linestyle='--', zorder=2)

# ================= ANA YÖNLENDİRİCİ =================
def generate_3d_chart_base64(df: pd.DataFrame, axes: dict, chart_type: str = "3D Scatter Plot", data_processing: str = "none") -> dict:
    plot_df = df.copy()
    plot_df = plot_df.replace(['none', 'None', 'nan', 'NaN', ''], np.nan)

    plot_df.columns = plot_df.columns.str.strip()
    valid_cols = list(plot_df.columns)
    
    x_raw = str(axes.get("x", "")).strip()
    y_raw = str(axes.get("y", "")).strip()
    z_raw = str(axes.get("z", "")).strip()
    c_raw = str(axes.get("color", "")).strip()
    s_raw = str(axes.get("size", "")).strip()
    
    def find_best_col(target, valid_list):
        if not target or target == "None": return None
        if target in valid_list: return target
        for col in valid_list:
            if col.lower() == target.lower(): return col
        return None

    x_col = find_best_col(x_raw, valid_cols)
    if not x_col: x_col = valid_cols[0]
        
    y_col = find_best_col(y_raw, valid_cols)
    if not y_col: y_col = valid_cols[1] if len(valid_cols)>1 else valid_cols[0]
        
    z_col = find_best_col(z_raw, valid_cols)
    if not z_col: z_col = valid_cols[2] if len(valid_cols)>2 else valid_cols[0]
        
    color_col = find_best_col(c_raw, valid_cols)
    size_col = find_best_col(s_raw, valid_cols)

    if data_processing and str(data_processing).lower() != "none" and str(data_processing).startswith("top_"):
        try:
            top_n = int(str(data_processing).split("_")[1])
            for col in [x_col, y_col, z_col, color_col]:
                if col and col in plot_df.columns:
                    if not pd.api.types.is_numeric_dtype(plot_df[col]):
                        top_vals = plot_df[col].value_counts().nlargest(top_n).index
                        plot_df = plot_df[plot_df[col].isin(top_vals)]
        except Exception as e:
            print(f"Filtreleme hatası (Yoksayıldı): {e}")

    logical_orders = {
        'low_high': ['poor', 'low', 'fair', 'medium', 'good', 'high', 'excellent', 'extreme'],
        'yes_no': ['no', 'false', 'yes', 'true']
    }

    label_mappings = {}
    for col in [x_col, y_col, z_col, color_col, size_col]:
        if col and col in plot_df.columns:
            if not pd.api.types.is_numeric_dtype(plot_df[col]):
                plot_df[col] = plot_df[col].astype(str)
                unique_vals = plot_df[col].dropna().unique().tolist()
                
                def get_sort_weight(val):
                    v_lower = str(val).lower().strip()
                    if v_lower in logical_orders['low_high']: return logical_orders['low_high'].index(v_lower)
                    if v_lower in logical_orders['yes_no']: return logical_orders['yes_no'].index(v_lower)
                    return 999 
                    
                sorted_vals = sorted(unique_vals, key=lambda x: (get_sort_weight(x), x))
                plot_df[col] = pd.Categorical(plot_df[col], categories=sorted_vals, ordered=True)
                label_mappings[col] = [str(cat).replace('_', ' ').title()[:12] for cat in sorted_vals]
                plot_df[col] = plot_df[col].cat.codes

    for col in plot_df.columns:
        if pd.api.types.is_numeric_dtype(plot_df[col]):
            plot_df[col] = plot_df[col].fillna(plot_df[col].mean())

    # 🚀 KRİTİK DÜZELTME: AR İLE AYNI SİSTEMATİK ATLAMAYI YAPAN ALGORİTMA 🚀
    point_limit = 2500
    if len(plot_df) > point_limit:
        data_step = max(1, len(plot_df) // point_limit)
        plot_df = plot_df.iloc[::data_step].copy()

    fig = plt.figure(figsize=(8, 7), dpi=250)
    ax = fig.add_subplot(111, projection='3d')
    ax.view_init(elev=25, azim=135)
    try: ax.set_box_aspect(None)
    except: pass 

    cmap = 'plasma' if color_col else None

    if "Bubble" in chart_type:
        if size_col and size_col in plot_df.columns:
            x_data = np.array(plot_df[x_col], dtype=float)
            y_data = np.array(plot_df[y_col], dtype=float)
            z_data = np.array(plot_df[z_col], dtype=float)
            c_data = np.array(plot_df[color_col], dtype=float) if color_col and color_col in plot_df.columns else '#5E35B1'
            s_data = np.array(plot_df[size_col], dtype=float)
        else:
            raw_cols = [x_col, y_col, z_col, color_col]
            group_cols = list(dict.fromkeys([c for c in raw_cols if c and c in plot_df.columns]))
            freq_df = plot_df.groupby(group_cols, dropna=False).size().reset_index(name='count')
            x_data = np.array(freq_df[x_col], dtype=float)
            y_data = np.array(freq_df[y_col], dtype=float)
            z_data = np.array(freq_df[z_col], dtype=float)
            c_data = np.array(freq_df[color_col], dtype=float) if color_col and color_col in freq_df.columns else '#5E35B1'
            s_data = np.array(freq_df['count'], dtype=float)
    else:
        x_data = np.array(plot_df[x_col], dtype=float)
        y_data = np.array(plot_df[y_col], dtype=float)
        z_data = np.array(plot_df[z_col], dtype=float)
        c_data = np.array(plot_df[color_col], dtype=float) if color_col and color_col in plot_df.columns else '#5E35B1'
        s_data = 45 

    sm_voxel = None 
    cbar_obj = None 
    c_min, c_max = 0, 0 

    try:
        if "Line" in chart_type or "Trajectory" in chart_type: 
            draw_line(ax, x_col, y_col, z_col, color_col, plot_df, z_data.min() if len(z_data)>0 else 0)
        elif "Voxel" in chart_type or "Density" in chart_type: 
            sm_voxel = draw_voxel(ax, x_data, y_data, z_data, cmap)
        elif "Network" in chart_type or "Graph" in chart_type: 
            draw_network(ax, x_col, y_col, z_col, color_col, plot_df, cmap)
        else: 
            draw_scatter(ax, x_data, y_data, z_data, c_data, s_data, cmap, "Bubble" in chart_type)
    except Exception as e:
        print(f"Hata: {e}")
        ax.scatter(x_data, y_data, z_data, c='#5E35B1', s=45, alpha=0.5, edgecolors='white', linewidth=0.2)

    if sm_voxel:
        cbar_obj = fig.colorbar(sm_voxel, ax=ax, pad=0.18, shrink=0.6)
        cbar_obj.set_label("Frekans (Yigilma)", fontsize=11, fontweight='bold')
        c_min, c_max = sm_voxel.norm.vmin, sm_voxel.norm.vmax
    elif color_col and "Voxel" not in chart_type and "Density" not in chart_type:
        if color_col in label_mappings:
            lbls_c = label_mappings[color_col]
            n_colors = len(lbls_c)
            try: cmap_discrete = plt.cm.get_cmap(cmap, n_colors)
            except: cmap_discrete = plt.get_cmap(cmap, n_colors)
            norm = plt.Normalize(vmin=-0.5, vmax=n_colors - 0.5)
            sm = plt.cm.ScalarMappable(cmap=cmap_discrete, norm=norm)
            sm.set_array([]) 
            cbar_obj = fig.colorbar(sm, ax=ax, pad=0.18, shrink=0.6, ticks=range(n_colors))
            cbar_obj.ax.set_yticklabels(lbls_c) 
            c_min, c_max = 0, n_colors - 1
        else:
            sm = plt.cm.ScalarMappable(cmap=cmap, norm=plt.Normalize(vmin=c_data.min(), vmax=c_data.max()))
            sm.set_array([]) 
            cbar_obj = fig.colorbar(sm, ax=ax, pad=0.18, shrink=0.6)
            c_min, c_max = c_data.min(), c_data.max()

    ax.set_xlabel(x_col, fontsize=11, labelpad=5)
    ax.set_ylabel(y_col, fontsize=11, labelpad=5)
    ax.set_zlabel(z_col, fontsize=11, labelpad=5)

    tick_fontsize = 6.5 
    
    if x_col in label_mappings: 
        lbls = label_mappings[x_col]
        step = max(1, len(lbls) // 6) 
        ax.set_xticks(range(0, len(lbls), step))
        ax.set_xticklabels([lbls[i] for i in range(0, len(lbls), step)], rotation=45, ha='right', va='center', fontsize=tick_fontsize)
    else: 
        ax.xaxis.set_major_locator(MaxNLocator(5))
        plt.setp(ax.get_xticklabels(), rotation=45, ha='right', va='center', fontsize=tick_fontsize)
        
    if y_col in label_mappings: 
        lbls = label_mappings[y_col]
        step = max(1, len(lbls) // 6)
        ax.set_yticks(range(0, len(lbls), step))
        ax.set_yticklabels([lbls[i] for i in range(0, len(lbls), step)], rotation=-25, ha='left', va='center', fontsize=tick_fontsize)
    else: 
        ax.yaxis.set_major_locator(MaxNLocator(5))
        plt.setp(ax.get_yticklabels(), rotation=-25, ha='left', va='center', fontsize=tick_fontsize)

    if z_col in label_mappings: 
        lbls = label_mappings[z_col]
        step = max(1, len(lbls) // 6)
        ax.set_zticks(range(0, len(lbls), step))
        ax.set_zticklabels([lbls[i] for i in range(0, len(lbls), step)], rotation=15, ha='right', va='center', fontsize=tick_fontsize)
    else: 
        ax.zaxis.set_major_locator(MaxNLocator(5))
        plt.setp(ax.get_zticklabels(), rotation=15, ha='right', va='center', fontsize=tick_fontsize)

    ax.tick_params(axis='x', pad=1)
    ax.tick_params(axis='y', pad=1)
    ax.tick_params(axis='z', pad=1)

    ax.xaxis.pane.fill = False; ax.yaxis.pane.fill = False; ax.zaxis.pane.fill = False
    plt.subplots_adjust(left=0.1, right=0.9, bottom=0.1, top=0.9)

    fig.canvas.draw()
    
    try:
        xlim = ax.get_xlim3d()
        ylim = ax.get_ylim3d()
        zlim = ax.get_zlim3d()
    except:
        xlim = ax.get_xlim()
        ylim = ax.get_ylim()
        zlim = ax.get_zlim()

    x_min, x_max = min(xlim), max(xlim)
    y_min, y_max = min(ylim), max(ylim)
    z_min, z_max = min(zlim), max(zlim)

    x_ticks = [float(t) for t in ax.get_xticks() if x_min <= t <= x_max]
    y_ticks = [float(t) for t in ax.get_yticks() if y_min <= t <= y_max]
    z_ticks = [float(t) for t in ax.get_zticks() if z_min <= t <= z_max]

    c_ticks = []
    if cbar_obj is not None:
        c_ticks = [float(t) for t in cbar_obj.get_ticks() if c_min <= t <= c_max]

    buffer = io.BytesIO()
    plt.savefig(buffer, format='png', dpi=250, bbox_inches='tight', transparent=True)
    plt.close(fig); buffer.seek(0)
    
    return {
        "chart_image_base64": base64.b64encode(buffer.read()).decode('utf-8'),
        "scales": {
            "x": x_ticks,
            "y": y_ticks,
            "z": z_ticks,
            "c": c_ticks 
        }
    }